import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpServer;
import java.io.ByteArrayOutputStream;
import java.net.InetSocketAddress;
import java.net.Socket;
import java.net.SocketTimeoutException;
import java.net.URL;
import java.net.URLClassLoader;
import java.nio.charset.StandardCharsets;
import java.nio.file.Path;
import java.util.List;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.atomic.AtomicReference;
import java.util.function.Predicate;
import java.util.zip.ZipEntry;
import java.util.zip.ZipOutputStream;

public final class StalledFallbackTest {
    private static final String HELPER = "com.moonsworth.lunar.ICCRRCCIOOIRICIHHCIOROHHROOHHR.RCHICCCIHRCRCHCIRRIIRRHICORHHO.OOIOHHHCIICRRRCICOIIIRRRICOCIC";
    private static final String DOWNLOAD = "CRRHRIRIHOHRHRCCHRCOICHOOIHOIC";
    private static final int ORIGINAL_LIMIT_SECONDS = 30;

    public static void main(String[] args) {
        try {
            run(args);
            System.exit(0);
        } catch (Throwable failure) {
            System.out.println("FAIL type=" + failure.getClass().getSimpleName() + " reason=" + safeReason(failure));
            if (failure.getCause() != null) System.out.println("causeType=" + failure.getCause().getClass().getSimpleName());
            System.exit(1);
        }
    }

    private static String safeReason(Throwable failure) {
        String message = failure.getMessage();
        return message == null ? "unspecified" : message.replaceAll("[^A-Za-z0-9 _-]", "");
    }

    private static void run(String[] args) throws Exception {
        if (args.length != 2 || !(args[0].equals("original") || args[0].equals("candidate")))
            throw new IllegalArgumentException("mode and jar required");
        boolean candidate = args[0].equals("candidate");
        requireSilentConnectTimeout();
        try (URLClassLoader loader = new URLClassLoader(new URL[]{Path.of(args[1]).toUri().toURL()}, StalledFallbackTest.class.getClassLoader())) {
            AtomicInteger fallbackRequests = new AtomicInteger();
            byte[] archive = tinyZip();
            HttpServer fallback = HttpServer.create(new InetSocketAddress("127.0.0.1", 0), 0);
            fallback.createContext("/", exchange -> {
                fallbackRequests.incrementAndGet();
                respond(exchange, 200, archive);
            });
            fallback.start();
            try {
                Class<?> helper = Class.forName(HELPER, true, loader);
                var method = helper.getDeclaredMethod(DOWNLOAD, List.class, String.class, Predicate.class);
                method.setAccessible(true);
                CountDownLatch done = new CountDownLatch(1);
                AtomicReference<Throwable> failure = new AtomicReference<>();
                Thread caller = new Thread(() -> {
                    try {
                        method.invoke(null,
                            List.of("http://192.0.2.1:443/", base(fallback)),
                            "fixture.zip", (Predicate<byte[]>) bytes -> bytes.length > 3 && bytes[0] == 'P');
                    } catch (Throwable error) {
                        failure.set(error);
                    } finally {
                        done.countDown();
                    }
                }, "stalled-downloader-caller");
                caller.setDaemon(true);
                long started = System.nanoTime();
                caller.start();
                boolean completed = done.await(ORIGINAL_LIMIT_SECONDS, TimeUnit.SECONDS);
                long elapsed = TimeUnit.NANOSECONDS.toMillis(System.nanoTime() - started);
                String callerState = caller.getState().name();
                boolean sendFrame = hasFrame(caller, "jdk.internal.net.http.HttpClientImpl", "send");
                boolean helperFrame = hasFrame(caller, HELPER, DOWNLOAD);

                if (candidate) {
                    if (!completed || failure.get() != null || fallbackRequests.get() != 1)
                        throw new AssertionError("candidate fallback did not finish exactly once");
                    if (elapsed >= 25000) throw new AssertionError("candidate fallback exceeded 25s");
                    System.out.println("PASS mode=candidate firstConnect=timed-out helperCompleted=true fallbackRequests=1 elapsedSeconds=" + elapsed / 1000.0);
                } else {
                    String networkStates = networkStates();
                    if (completed || fallbackRequests.get() != 0 || !helperFrame || !callerState.equals("WAITING") || !sendFrame)
                        throw new AssertionError("original did not remain inside the stalled download call");
                    System.out.println("PASS mode=original firstConnect=timed-out helperCompleted=false fallbackRequests=0 elapsedSeconds=" + elapsed / 1000.0 + " callerState=" + callerState + " sendFrame=true downloaderFrame=true networkStates=" + networkStates);
                }
            } finally {
                fallback.stop(0);
            }
        }
    }

    private static void requireSilentConnectTimeout() throws Exception {
        try (Socket probe = new Socket()) {
            try {
                probe.connect(new InetSocketAddress("192.0.2.1", 443), 3000);
                throw new IllegalStateException("TEST-NET endpoint unexpectedly accepted a TCP connection");
            } catch (SocketTimeoutException stalled) {
                System.out.println("fixture destination=TEST-NET connect=timed-out timeoutSeconds=3");
            }
        }
    }

    private static boolean hasFrame(Thread caller, String className, String methodName) {
        for (StackTraceElement frame : caller.getStackTrace())
            if (frame.getClassName().equals(className) && frame.getMethodName().equals(methodName)) return true;
        return false;
    }

    private static String networkStates() throws Exception {
        long pid = ProcessHandle.current().pid();
        Process netstat = new ProcessBuilder("netstat.exe", "-ano").redirectErrorStream(true).start();
        java.util.Set<String> states = new java.util.TreeSet<>();
        try (var lines = new java.io.BufferedReader(new java.io.InputStreamReader(netstat.getInputStream()))) {
            String line;
            while ((line = lines.readLine()) != null) {
                String[] fields = line.trim().split("\\s+");
                if (fields.length >= 5 && fields[fields.length - 1].equals(Long.toString(pid)) && fields[0].equalsIgnoreCase("TCP"))
                    states.add(fields[fields.length - 2]);
            }
        }
        netstat.waitFor(3, TimeUnit.SECONDS);
        netstat.destroyForcibly();
        return states.isEmpty() ? "unobserved" : String.join(",", states);
    }

    private static String base(HttpServer server) { return "http://127.0.0.1:" + server.getAddress().getPort() + "/"; }
    private static void respond(HttpExchange exchange, int status, byte[] body) throws java.io.IOException {
        exchange.sendResponseHeaders(status, body.length);
        exchange.getResponseBody().write(body);
        exchange.close();
    }
    private static byte[] tinyZip() throws Exception {
        ByteArrayOutputStream bytes = new ByteArrayOutputStream();
        try (ZipOutputStream zip = new ZipOutputStream(bytes)) {
            zip.putNextEntry(new ZipEntry("fixture.txt"));
            zip.write("ok".getBytes(StandardCharsets.UTF_8));
            zip.closeEntry();
        }
        return bytes.toByteArray();
    }
}

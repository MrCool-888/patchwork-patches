import com.sun.net.httpserver.HttpServer;
import java.io.ByteArrayOutputStream;
import java.net.InetSocketAddress;
import java.net.Socket;
import java.net.SocketTimeoutException;
import java.net.URL;
import java.net.URLClassLoader;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.zip.ZipEntry;
import java.util.zip.ZipFile;
import java.util.zip.ZipOutputStream;

public final class BatchFallbackTest {
    public static void main(String[] args) {
        try { run(args); System.exit(0); }
        catch (Throwable e) { System.out.println("FAIL " + e.getClass().getSimpleName()); System.exit(1); }
    }

    private static void run(String[] args) throws Exception {
        if (args.length != 3 || !(args[0].equals("previous") || args[0].equals("candidate")))
            throw new IllegalArgumentException("mode jar fresh-cache");
        try (Socket socket = new Socket()) {
            try { socket.connect(new InetSocketAddress("192.0.2.1", 443), 3000); throw new AssertionError("TEST-NET accepted connection"); }
            catch (SocketTimeoutException expected) { }
        }
        Path cache = Path.of(args[2]);
        if (Files.exists(cache)) throw new IllegalArgumentException("Fresh cache required");
        byte[] zip;
        ByteArrayOutputStream bytes = new ByteArrayOutputStream();
        try (ZipOutputStream z = new ZipOutputStream(bytes)) {
            z.putNextEntry(new ZipEntry("fixture.txt")); z.write(new byte[]{1}); z.closeEntry();
        }
        zip = bytes.toByteArray();
        AtomicInteger hits = new AtomicInteger();
        HttpServer server = HttpServer.create(new InetSocketAddress("127.0.0.1", 0), 0);
        server.createContext("/", x -> {
            hits.incrementAndGet(); x.sendResponseHeaders(200, zip.length);
            try (var out = x.getResponseBody()) { out.write(zip); }
        });
        server.start();
        try (URLClassLoader loader = new URLClassLoader(new URL[]{Path.of(args[1]).toUri().toURL()})) {
            Class<?> maven = Class.forName("com.moonsworth.lunar.IIICIHORHCRHRHIRCHRCRHRICCROOC.CRRHRIRIHOHRHRCCHRCOICHOOIHOIC", true, loader);
            var field = maven.getDeclaredField("url"); field.setAccessible(true);
            Object[] repositories = maven.getEnumConstants();
            // Only this isolated JVM's repository instances change; no OS routing or live cache changes.
            for (int i = 0; i < repositories.length; i++)
                field.set(repositories[i], i == 0 ? "http://192.0.2.1:443/" : "http://127.0.0.1:" + server.getAddress().getPort() + "/");
            List<Long> times = new ArrayList<>();
            for (int i = 0; i < 3; i++) {
                long start = System.nanoTime();
                Path artifact = (Path)maven.getMethod("download", String.class, Path.class).invoke(null, "fixture:artifact"+i+":1", cache);
                times.add((System.nanoTime()-start)/1000000L);
                try (ZipFile z = new ZipFile(artifact.toFile())) {
                    if (z.getEntry("fixture.txt") == null) throw new AssertionError("Invalid archive");
                }
            }
            if (hits.get() != 3 || times.get(0) < 14000 || times.get(0) > 25000) throw new AssertionError("First fallback mismatch");
            if (args[0].equals("candidate")) {
                if (times.get(1) > 2000 || times.get(2) > 2000) throw new AssertionError("Repeated connection waits retained");
            } else if (times.get(1) < 14000 || times.get(2) < 14000) throw new AssertionError("Previous delay not reproduced");
            System.out.println("PASS mode=" + args[0] + " downloads=3 validArchives=3 fallbackHits=" + hits.get() + " elapsedMillis=" + times);
        } finally { server.stop(0); }
    }
}

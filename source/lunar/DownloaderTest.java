import com.sun.net.httpserver.HttpExchange;
import com.sun.net.httpserver.HttpServer;
import java.io.ByteArrayOutputStream;
import java.net.InetSocketAddress;
import java.net.URL;
import java.net.URLClassLoader;
import java.nio.file.Files;
import java.nio.file.Path;
import java.time.Duration;
import java.util.List;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.function.Predicate;
import java.util.zip.ZipEntry;
import java.util.zip.ZipOutputStream;

public final class DownloaderTest {
  private static final String HELPER = "com.moonsworth.lunar.ICCRRCCIOOIRICIHHCIOROHHROOHHR.RCHICCCIHRCRCHCIRRIIRRHICORHHO.OOIOHHHCIICRRRCICOIIIRRRICOCIC";
  private static int checks;

  public static void main(String[] args) throws Exception {
    try {
    if (args.length != 2) throw new IllegalArgumentException("mode jar-path");
    boolean candidate = args[0].equals("candidate");
    if (!candidate && !args[0].equals("original")) throw new IllegalArgumentException("mode must be original or candidate");
    try (URLClassLoader loader = new URLClassLoader(new URL[]{Path.of(args[1]).toUri().toURL()}, DownloaderTest.class.getClassLoader())) {
      Class<?> helper = Class.forName(HELPER, true, loader);
      duration(helper, "RCIHIRICIICHCIOHHOORHHICIIORII", candidate ? 15 : 180);
      duration(helper, "OCRICHORCHRCIIIRHRIICIRIROCCHH", 180);
      FieldCheck.checkHttpClient(helper, candidate ? 15 : 180);
      testStatusAndRepoRetry(helper, candidate);
      testNotFoundAndPredicate(helper, candidate);
      testExhaustion(helper);
      testMavenCacheBypass(loader, Path.of(args[1]));
    }
    System.out.println("PASS mode=" + args[0] + " assertions=" + checks);
    System.exit(0); // The helper's downloader executor is non-daemon.
    } catch (Throwable failure) {
      failure.printStackTrace();
      System.exit(1);
    }
  }

  private static void duration(Class<?> c, String name, long seconds) throws Exception {
    var f = c.getDeclaredField(name); f.setAccessible(true);
    Object value = f.get(null);
    check(value instanceof Duration && ((Duration)value).equals(Duration.ofSeconds(seconds)), name + "=" + value + " expected PT" + seconds + "S");
    System.out.println("duration " + name + "=" + value);
  }

  private static void testStatusAndRepoRetry(Class<?> helper, boolean candidate) throws Exception {
    AtomicInteger first = new AtomicInteger(), second = new AtomicInteger();
    byte[] zip = tinyZip();
    HttpServer one = server((x) -> { first.incrementAndGet(); respond(x, 503, new byte[0]); });
    HttpServer two = server((x) -> { second.incrementAndGet(); respond(x, 200, zip); });
    try {
      invoke(helper, List.of(base(one), base(two)), "artifact.zip", b -> b.length > 0);
      check(first.get() == (candidate ? 1 : 9), "first repository attempts=" + first + " candidate=" + candidate);
      check(second.get() == 1, "second repository attempts=" + second);
      System.out.println("retry firstRepo=" + first + " secondRepo=" + second);
    } finally { one.stop(0); two.stop(0); }
  }

  private static void testNotFoundAndPredicate(Class<?> helper, boolean candidate) throws Exception {
    AtomicInteger notFound = new AtomicInteger(), rejected = new AtomicInteger(), accepted = new AtomicInteger();
    byte[] zip = tinyZip();
    HttpServer a = server(x -> { notFound.incrementAndGet(); respond(x, 404, new byte[0]); });
    HttpServer b = server(x -> { rejected.incrementAndGet(); respond(x, 200, new byte[]{1,2,3}); });
    HttpServer c = server(x -> { accepted.incrementAndGet(); respond(x, 200, zip); });
    try {
      invoke(helper, List.of(base(a), base(b), base(c)), "artifact.zip", bytes -> bytes.length > 3 && bytes[0] == 'P');
      check(notFound.get() == 1, "404 should skip without retries: " + notFound);
      check(rejected.get() == (candidate ? 1 : 9), "predicate retries=" + rejected + " candidate=" + candidate);
      check(accepted.get() == 1, "accepted body should finish: " + accepted);
      System.out.println("skip404=" + notFound + " predicateRejected=" + rejected + " accepted=" + accepted);
    } finally { a.stop(0); b.stop(0); c.stop(0); }
  }

  private static void testExhaustion(Class<?> helper) throws Exception {
    AtomicInteger hits = new AtomicInteger();
    HttpServer server = server(x -> { hits.incrementAndGet(); respond(x, 200, new byte[]{1}); });
    try {
      try {
        invoke(helper, List.of(base(server)), "artifact.zip", bytes -> false);
        throw new AssertionError("expected helper exhaustion error");
      } catch (java.lang.reflect.InvocationTargetException ex) {
        Throwable cause = ex.getCause();
        check(cause instanceof RuntimeException || cause instanceof java.io.IOException,
            "unexpected exhaustion error=" + cause);
        check(cause.getMessage() != null && !cause.getMessage().isBlank(), "helper error message should be preserved");
        System.out.println("exhaustion type=" + cause.getClass().getName() + " attempts=" + hits.get());
      }
      check(hits.get() == (helper.getDeclaredField("RCIHIRICIICHCIOHHOORHHICIIORII").get(null).equals(Duration.ofSeconds(15)) ? 1 : 45), "exhaustion requests=" + hits);
    } finally { server.stop(0); }
  }

  private static void testMavenCacheBypass(ClassLoader loader, Path sourceJar) throws Exception {
    Path testRoot = Path.of(System.getProperty("lunar.testRoot", "work/lunar-downloader-tests"));
    Files.createDirectories(testRoot);
    Path root = Files.createTempDirectory(testRoot, "maven-cache-");
    Path cached = root.resolve("test/demo/1/demo-1.jar");
    Files.createDirectories(cached.getParent());
    Files.copy(sourceJar, cached);
    try {
      Class<?> checker = Class.forName(HELPER, true, loader);
      var valid = checker.getDeclaredMethod("OCOHCRCOCCIRICHIIORHHIIRHCCHIO", Path.class);
      check(Boolean.FALSE.equals(valid.invoke(null, cached)), "fixture jar must pass production cache validation");
      Class<?> maven = Class.forName("com.moonsworth.lunar.IIICIHORHCRHRHIRCHRCRHRICCROOC.CRRHRIRIHOHRHRCCHRCOICHOOIHOIC", true, loader);
      var download = maven.getDeclaredMethod("download", String.class, Path.class);
      Object result = download.invoke(null, "test:demo:1", root);
      check(Path.of(result.toString()).toAbsolutePath().normalize().equals(cached.toAbsolutePath().normalize()), "download should return the valid cached file: " + result);
      check(Files.size(cached) > 0, "cached artifact remains intact");
      System.out.println("cache bypass returned=" + result + " valid=true network-free fixture");
    } finally {
      try (var paths = Files.walk(root)) {
        paths.sorted(java.util.Comparator.reverseOrder()).forEach(path -> { try { Files.deleteIfExists(path); } catch (java.io.IOException e) { throw new java.io.UncheckedIOException(e); } });
      }
    }
  }

  private static void invoke(Class<?> c, List<String> repos, String artifact, Predicate<byte[]> predicate) throws Exception {
    var m = c.getDeclaredMethod("CRRHRIRIHOHRHRCCHRCOICHOOIHOIC", List.class, String.class, Predicate.class);
    m.invoke(null, repos, artifact, predicate);
  }

  private static HttpServer server(com.sun.net.httpserver.HttpHandler handler) throws Exception {
    HttpServer s = HttpServer.create(new InetSocketAddress("127.0.0.1", 0), 0); s.createContext("/", handler); s.start(); return s;
  }
  private static String base(HttpServer s) { return "http://127.0.0.1:" + s.getAddress().getPort() + "/"; }
  private static void respond(HttpExchange x, int code, byte[] body) throws java.io.IOException {
    x.sendResponseHeaders(code, body.length == 0 ? -1 : body.length);
    if (body.length != 0) x.getResponseBody().write(body);
    x.close();
  }
  private static byte[] tinyZip() throws Exception {
    ByteArrayOutputStream b = new ByteArrayOutputStream();
    try (ZipOutputStream z = new ZipOutputStream(b)) { z.putNextEntry(new ZipEntry("fixture.txt")); z.write("ok".getBytes(java.nio.charset.StandardCharsets.UTF_8)); z.closeEntry(); }
    return b.toByteArray();
  }
  private static void check(boolean condition, String message) { checks++; if (!condition) throw new AssertionError(message); }

  private static final class FieldCheck {
    static void checkHttpClient(Class<?> helper, long seconds) throws Exception {
      var f = helper.getDeclaredField("CRROCRCOIOIRCHORIHOOIHRRORHIOO"); f.setAccessible(true);
      Object client = f.get(null);
      var connect = java.net.http.HttpClient.class.getMethod("connectTimeout").invoke(client);
      check(connect instanceof java.util.Optional<?> && ((java.util.Optional<?>)connect).orElseThrow().equals(Duration.ofSeconds(seconds)), "HttpClient connect timeout=" + connect);
      System.out.println("HttpClient connectTimeout=" + connect);
    }
  }
}

package local.lunar;

import java.io.IOException;
import java.net.Authenticator;
import java.net.CookieHandler;
import java.net.ProxySelector;
import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpConnectTimeoutException;
import java.net.http.HttpHeaders;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.net.http.HttpTimeoutException;
import java.time.Duration;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.Executor;
import java.util.concurrent.atomic.AtomicInteger;
import javax.net.ssl.SSLContext;
import javax.net.ssl.SSLParameters;
import javax.net.ssl.SSLSession;

public final class BootstrapDownloadsTest {
    private static int checks;

    public static void main(String[] args) throws Exception {
        sameOriginConnectTimeoutIsRemembered();
        originsAreIndependentAndNormalized();
        otherFailuresAreNotRemembered();
        successfulResponseIsReturned();
        System.out.println("PASS BootstrapDownloads checks=" + checks);
    }

    private static void sameOriginConnectTimeoutIsRemembered() throws Exception {
        FakeClient client = new FakeClient();
        HttpRequest first = request("http://EXAMPLE.test:80/a.jar");
        client.failure = new HttpConnectTimeoutException("fixture");
        expect(HttpConnectTimeoutException.class, () -> BootstrapDownloads.send(client, first, HttpResponse.BodyHandlers.ofString()));
        client.failure = null;
        expect(HttpConnectTimeoutException.class, () -> BootstrapDownloads.send(client,
                request("http://example.test/b.jar"), HttpResponse.BodyHandlers.ofString()));
        check(client.calls.get() == 1, "same normalized origin should be attempted once");
    }

    private static void originsAreIndependentAndNormalized() throws Exception {
        FakeClient client = new FakeClient();
        client.response = response(404, "missing");
        client.failure = new HttpConnectTimeoutException("fixture");
        expect(HttpConnectTimeoutException.class, () -> BootstrapDownloads.send(client,
                request("https://Case.test:443/one"), HttpResponse.BodyHandlers.ofString()));
        client.failure = null;
        BootstrapDownloads.send(client, request("http://case.test/two"), HttpResponse.BodyHandlers.ofString());
        BootstrapDownloads.send(client, request("https://case.test:444/three"), HttpResponse.BodyHandlers.ofString());
        BootstrapDownloads.send(client, request("https://[::1]:443/four"), HttpResponse.BodyHandlers.ofString());
        BootstrapDownloads.send(client, request("https://[::1]/five"), HttpResponse.BodyHandlers.ofString());
        check(client.calls.get() == 5, "scheme, effective port, and IPv6 origin handling");
        expect(HttpConnectTimeoutException.class, () -> BootstrapDownloads.send(client,
                request("https://CASE.TEST/another"), HttpResponse.BodyHandlers.ofString()));
        check(client.calls.get() == 5, "case/default-port equivalent should share origin");
    }
    private static void otherFailuresAreNotRemembered() throws Exception {
        for (Exception failure : List.of(new HttpTimeoutException("request timeout"), new IOException("other IO"), new InterruptedException("interrupted"))) {
            FakeClient client = new FakeClient();
            HttpRequest request = request("http://failure-" + checks + ".test/artifact");
            client.failure = failure;
            expect(failure.getClass(), () -> BootstrapDownloads.send(client, request, HttpResponse.BodyHandlers.ofString()));
            client.failure = null;
            BootstrapDownloads.send(client, request, HttpResponse.BodyHandlers.ofString());
            check(client.calls.get() == 2, "non-connect failure must not memoize: " + failure.getClass().getSimpleName());
        }
        FakeClient statuses = new FakeClient();
        statuses.response = response(404, "missing");
        BootstrapDownloads.send(statuses, request("http://status.test/404"), HttpResponse.BodyHandlers.ofString());
        statuses.response = response(503, "unavailable");
        BootstrapDownloads.send(statuses, request("http://status.test/503"), HttpResponse.BodyHandlers.ofString());
        check(statuses.calls.get() == 2, "404/503 responses must not memoize");
    }

    private static void successfulResponseIsReturned() throws Exception {
        FakeClient client = new FakeClient();
        HttpResponse<String> expected = response(200, "archive body");
        client.response = expected;
        HttpResponse<String> actual = BootstrapDownloads.send(client, request("http://success.test/file"), HttpResponse.BodyHandlers.ofString());
        check(actual == expected && actual.body().equals("archive body"), "successful response/body delegated intact");
    }

    private static HttpRequest request(String uri) { return HttpRequest.newBuilder(URI.create(uri)).GET().build(); }
    private static HttpResponse<String> response(int status, String body) {
        return new HttpResponse<>() {
            public int statusCode() { return status; }
            public HttpRequest request() { return BootstrapDownloadsTest.request("http://response.test/"); }
            public Optional<HttpResponse<String>> previousResponse() { return Optional.empty(); }
            public HttpHeaders headers() { return HttpHeaders.of(Map.of(), (a, b) -> true); }
            public String body() { return body; }
            public Optional<SSLSession> sslSession() { return Optional.empty(); }
            public URI uri() { return URI.create("http://response.test/"); }
            public HttpClient.Version version() { return HttpClient.Version.HTTP_1_1; }
        };
    }

    private static void expect(Class<?> type, Throwing action) throws Exception {
        try { action.run(); throw new AssertionError("expected " + type.getSimpleName()); }
        catch (Exception actual) { check(type.isInstance(actual), "expected " + type.getSimpleName() + " got " + actual); }
    }
    private static void check(boolean ok, String message) { checks++; if (!ok) throw new AssertionError(message); }
    @FunctionalInterface private interface Throwing { void run() throws Exception; }

    private static final class FakeClient extends HttpClient {
        final AtomicInteger calls = new AtomicInteger();
        volatile Exception failure;
        volatile HttpResponse<?> response = response(200, "ok");
        @SuppressWarnings("unchecked")
        public <T> HttpResponse<T> send(HttpRequest request, HttpResponse.BodyHandler<T> handler) throws IOException, InterruptedException {
            calls.incrementAndGet();
            if (failure instanceof IOException e) throw e;
            if (failure instanceof InterruptedException e) throw e;
            if (failure != null) throw new AssertionError(failure);
            return (HttpResponse<T>) response;
        }
        public <T> CompletableFuture<HttpResponse<T>> sendAsync(HttpRequest r, HttpResponse.BodyHandler<T> h) { throw new UnsupportedOperationException(); }
        public <T> CompletableFuture<HttpResponse<T>> sendAsync(HttpRequest r, HttpResponse.BodyHandler<T> h, HttpResponse.PushPromiseHandler<T> p) { throw new UnsupportedOperationException(); }
        public Optional<CookieHandler> cookieHandler() { return Optional.empty(); }
        public Optional<Duration> connectTimeout() { return Optional.empty(); }
        public Redirect followRedirects() { return Redirect.NEVER; }
        public Optional<ProxySelector> proxy() { return Optional.empty(); }
        public SSLContext sslContext() { return null; }
        public SSLParameters sslParameters() { return new SSLParameters(); }
        public Optional<Authenticator> authenticator() { return Optional.empty(); }
        public Version version() { return Version.HTTP_1_1; }
        public Optional<Executor> executor() { return Optional.empty(); }
    }
}

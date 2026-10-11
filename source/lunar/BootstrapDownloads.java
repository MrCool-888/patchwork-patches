package local.lunar;

import java.io.IOException;
import java.net.http.HttpClient;
import java.net.http.HttpConnectTimeoutException;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.net.URI;
import java.util.Locale;
import java.util.Set;
import java.util.concurrent.ConcurrentHashMap;

public final class BootstrapDownloads {
    private static final Set<Origin> FAILED_ORIGINS = ConcurrentHashMap.newKeySet();

    private BootstrapDownloads() {}

    public static <T> HttpResponse<T> send(HttpClient client, HttpRequest request,
                                            HttpResponse.BodyHandler<T> handler)
            throws IOException, InterruptedException {
        Origin origin = Origin.of(request.uri());
        if (FAILED_ORIGINS.contains(origin))
            throw new HttpConnectTimeoutException("A previous connection attempt to this repository origin timed out");
        try {
            return client.send(request, handler);
        } catch (HttpConnectTimeoutException timeout) {
            FAILED_ORIGINS.add(origin);
            throw timeout;
        }
    }

    private record Origin(String scheme, String host, int port) {
        static Origin of(URI uri) {
            String scheme = uri.getScheme();
            String host = uri.getHost();
            if (scheme == null || host == null)
                throw new IllegalArgumentException("Request URI must have a scheme and host");
            scheme = scheme.toLowerCase(Locale.ROOT);
            host = host.toLowerCase(Locale.ROOT);
            int port = uri.getPort();
            if (port < 0) port = scheme.equals("https") ? 443 : scheme.equals("http") ? 80 : -1;
            return new Origin(scheme, host, port);
        }
    }
}

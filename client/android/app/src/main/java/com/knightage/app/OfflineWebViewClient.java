package com.knightage.app;

import android.graphics.Bitmap;
import android.webkit.WebResourceError;
import android.webkit.WebResourceRequest;
import android.webkit.WebResourceResponse;
import android.webkit.WebView;
import com.getcapacitor.Bridge;
import com.getcapacitor.BridgeWebViewClient;

/**
 * Extends Capacitor's own WebViewClient (rather than replacing it) so its
 * local-server interception and errorPath handling keep working -- this only
 * adds main-frame-only failure/recovery notifications for the native offline
 * screen. Sub-frame/sub-resource errors (a failed font or tracking pixel) are
 * deliberately ignored so they can't flash the offline screen over a page
 * that actually loaded fine.
 */
public class OfflineWebViewClient extends BridgeWebViewClient {

    public interface Listener {
        void onMainFrameLoadStarted();

        void onMainFrameError();

        void onMainFrameLoaded();
    }

    private final Listener listener;

    public OfflineWebViewClient(Bridge bridge, Listener listener) {
        super(bridge);
        this.listener = listener;
    }

    @Override
    public void onPageStarted(WebView view, String url, Bitmap favicon) {
        super.onPageStarted(view, url, favicon);
        listener.onMainFrameLoadStarted();
    }

    @Override
    public void onReceivedError(WebView view, WebResourceRequest request, WebResourceError error) {
        super.onReceivedError(view, request, error);
        if (request.isForMainFrame()) {
            listener.onMainFrameError();
        }
    }

    @Override
    public void onReceivedHttpError(WebView view, WebResourceRequest request, WebResourceResponse errorResponse) {
        super.onReceivedHttpError(view, request, errorResponse);
        if (request.isForMainFrame() && errorResponse.getStatusCode() >= 500) {
            listener.onMainFrameError();
        }
    }

    @Override
    public void onPageFinished(WebView view, String url) {
        super.onPageFinished(view, url);
        listener.onMainFrameLoaded();
    }
}

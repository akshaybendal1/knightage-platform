import type { CapacitorConfig } from '@capacitor/cli';

// The combined "Knightage" Android app: one native shell for CRM, Inventory and Accounting. It opens the live
// Platform site, whose sign-in and "Your services" page are the launcher -- the user signs in once, picks an app,
// and Platform hands the session over with /auth-callback?token= (the same hand-off the web launcher uses).
// The three app sites are in allowNavigation, so they open inside this WebView instead of the external browser,
// and Capacitor 8 proxies them like the main site, so the native bridge (Google sign-in, push, splash) works
// there too. Same set-up as the single-app shells in knightage-crm / -inventory-sales / -accounting.
//
// KNIGHTAGE_APP_TARGET=local (only for emulator testing, with `adb reverse tcp:510x tcp:510x` for each port)
// points the shell at the local dev APIs instead. Never build a release with it set.
const local = process.env['KNIGHTAGE_APP_TARGET'] === 'local';

const config: CapacitorConfig = {
  appId: 'com.knightage.app',
  appName: 'Knightage',
  webDir: '../src/Knightage.Platform.Api/wwwroot/browser',
  server: local
    ? {
        url: 'http://localhost:5102',
        cleartext: true,
        // Capacitor uses each entry three ways: host mask for navigation ('localhost'), request interception that
        // injects the bridge (needs 'host:port' to match), and bridge origin rule ('https://' is prepended unless
        // a scheme is given). Production hosts have no port and are https, so they need only the plain host.
        allowNavigation: ['localhost', ...[5104, 5105, 5106].flatMap((port) => [`localhost:${port}`, `http://localhost:${port}`])],
      }
    : {
        url: 'https://platform.knightagetech.com',
        androidScheme: 'https',
        allowNavigation: ['crm.knightagetech.com', 'ias.knightagetech.com', 'account.knightagetech.com'],
      },
  // CRM's pages run in here too, so this carries CRM's native Google sign-in (Google blocks its web widget
  // inside WebViews). MainActivity adds the bridge to the three app sites -- see injectBridgeIntoAppOrigins.
  // Push notifications are deliberately NOT installed yet: without google-services.json for com.knightage.app
  // their registration kills Capacitor's plugin thread (later plugin calls, e.g. Google sign-in, would hang).
  // Register the app in Firebase first, then `npm i @capacitor/push-notifications@8.1.2`, add the
  // POST_NOTIFICATIONS permission and google-services.json, and rebuild. CRM skips push when it's absent.
  plugins: {
    SocialLogin: {
      providers: {
        google: true,
        facebook: false,
        apple: false,
        twitter: false,
      },
    },
    // Hidden as soon as the site finishes loading; launchShowDuration is only a floor so it never just flashes.
    SplashScreen: {
      launchShowDuration: 400,
      launchAutoHide: true,
      launchFadeOutDuration: 300,
      backgroundColor: '#FFFFFF',
      androidSplashResourceName: 'splash',
      androidScaleType: 'CENTER_INSIDE',
      showSpinner: true,
      spinnerColor: '#1572E8',
    },
  },
};

export default config;

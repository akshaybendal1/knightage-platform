declare const google: any;

const SCRIPT_SRC = 'https://accounts.google.com/gsi/client';

let loadPromise: Promise<void> | null = null;

/**
 * Resolves once window.google.accounts.id is ready to use. index.html loads the
 * GSI script with async/defer, which gives no guarantee it's finished by the time
 * a component's ngOnInit runs -- calling google.accounts.id.* before it has leaves
 * that reference undefined and throws, which (having been uncaught) aborts the rest
 * of that change-detection pass, breaking unrelated parts of the same page. This
 * makes the wait explicit instead of assuming the race is always won, since a
 * slower connection (mobile, an app WebView) loses it far more often than a fast
 * desktop one does.
 */
export function loadGoogleIdentityScript(): Promise<void> {
  if (loadPromise) {
    return loadPromise;
  }

  loadPromise = new Promise<void>((resolve, reject) => {
    if (typeof google !== 'undefined' && google?.accounts?.id) {
      resolve();
      return;
    }

    const existing = document.querySelector<HTMLScriptElement>(`script[src="${SCRIPT_SRC}"]`);
    if (existing) {
      existing.addEventListener('load', () => resolve());
      existing.addEventListener('error', () => reject(new Error('Failed to load Google Identity Services script.')));
      return;
    }

    const script = document.createElement('script');
    script.src = SCRIPT_SRC;
    script.async = true;
    script.onload = () => resolve();
    script.onerror = () => reject(new Error('Failed to load Google Identity Services script.'));
    document.head.appendChild(script);
  });

  return loadPromise;
}

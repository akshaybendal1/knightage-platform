// Switching to one of the apps is a full page load, so this page would otherwise sit there unchanged until the app's
// first paint. Cover it straight away with the screen the app shows while it loads (its index.html .boot-splash, same
// name and accent) -- the same as knightage-apps.ts's showAppSwitchScreen in knightage-crm / -inventory-sales /
// -accounting. Removed again if the browser restores this page from its back/forward cache (Back from the app).
export function showAppSwitchScreen(appName: string): void {
  const el = document.createElement('div');
  el.className = 'boot-splash';
  el.setAttribute('role', 'status');
  el.setAttribute('aria-live', 'polite');
  el.style.setProperty('--boot-accent', '#e66239');
  const mark = document.createElement('div');
  mark.className = 'boot-splash-mark';
  mark.append('Knightage', Object.assign(document.createElement('span'), { textContent: appName }));
  const spinner = document.createElement('div');
  spinner.className = 'boot-splash-spinner';
  spinner.setAttribute('aria-hidden', 'true');
  const text = Object.assign(document.createElement('div'), { className: 'boot-splash-text', textContent: `Opening ${appName}…` });
  el.append(mark, spinner, text);
  document.body.appendChild(el);
  window.addEventListener('pageshow', (event) => event.persisted && el.remove());
}

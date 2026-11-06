import { AccessAppDef, AccessFeatureDef, ConfigurableRole } from './models';

// How the Users screen reads access: a user gets what their role allows (the catalog's defaults) unless an
// exception is stored for them. Keys: "Crm" (may open the app), "Crm:leads:add" (may add leads).

export const appKey = (service: string) => service;

export const actionKey = (service: string, feature: string, action: string) => `${service}:${feature}:${action}`;

export function isConfigurableRole(role: string): role is ConfigurableRole {
  return role === 'Admin' || role === 'Member' || role === 'Sales';
}

/** What the role gives for an area when nothing is stored: every action on it, or none. Opening an app: yes. */
export function roleDefault(feature: AccessFeatureDef, role: string): boolean {
  if (role === 'Owner') {
    return true;
  }
  return isConfigurableRole(role) ? feature.defaults[role] : false;
}

/** Every key the screen can set for a user, with the value their role gives by default. */
export function defaultsFor(apps: AccessAppDef[], role: string): Map<string, boolean> {
  const map = new Map<string, boolean>();
  for (const app of apps) {
    map.set(appKey(app.service), true);
    for (const feature of app.features) {
      const value = roleDefault(feature, role);
      for (const action of feature.actions) {
        map.set(actionKey(app.service, feature.key, action.key), value);
      }
    }
  }
  return map;
}

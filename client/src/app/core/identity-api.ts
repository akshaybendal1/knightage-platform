import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { AppConfig } from './app-config';

export interface OrganizationSummary {
  id: string;
  name: string;
  contactEmail?: string | null;
  contactPhone?: string | null;
  address?: string | null;
}

// Calls knightage-identity directly (not proxied through this app's own backend) --
// every Knightage app shares the same JWT issuer/signing key, so the token already
// attached by authInterceptor is accepted there regardless of which app is calling.
@Injectable({
  providedIn: 'root',
})
export class IdentityApi {
  constructor(
    private readonly http: HttpClient,
    private readonly appConfig: AppConfig,
  ) {}

  getMyOrganization() {
    return this.http.get<OrganizationSummary>(`${this.appConfig.identityBaseUrl}/api/organizations/me`);
  }
}

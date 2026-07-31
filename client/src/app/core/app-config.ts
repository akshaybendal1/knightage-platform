import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

export interface ClientConfig {
  identityBaseUrl: string;
}

/**
 * Fetched from this app's own backend (GET /api/client-config) on startup via
 * provideAppInitializer, rather than baked into the Angular build at compile time.
 * Same pattern as knightage-accounting's and knightage-crm's AppConfig.
 */
@Injectable({
  providedIn: 'root',
})
export class AppConfig {
  private readonly configState = signal<ClientConfig | null>(null);

  constructor(private readonly http: HttpClient) {}

  async load(): Promise<void> {
    const config = await firstValueFrom(this.http.get<ClientConfig>('/api/client-config'));
    this.configState.set(config);
  }

  get identityBaseUrl(): string {
    const config = this.configState();
    if (!config) {
      throw new Error('AppConfig was read before load() completed -- check the app initializer is registered.');
    }
    return config.identityBaseUrl;
  }
}

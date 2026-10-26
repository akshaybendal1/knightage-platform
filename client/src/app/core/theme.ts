import { Injectable, signal } from '@angular/core';

export type ThemeName = 'light' | 'dark';

const STORAGE_KEY = 'knightage_platform_theme';

/**
 * Defaults to dark for anyone without a stored preference -- matches
 * knightage-crm's default. A returning visitor's explicit choice always
 * wins over that default.
 */
const DEFAULT_THEME: ThemeName = 'dark';

@Injectable({
  providedIn: 'root',
})
export class Theme {
  readonly current = signal<ThemeName>(this.loadStored() ?? DEFAULT_THEME);

  constructor() {
    this.apply(this.current());
  }

  toggle(): void {
    this.set(this.current() === 'dark' ? 'light' : 'dark');
  }

  set(theme: ThemeName): void {
    this.current.set(theme);
    localStorage.setItem(STORAGE_KEY, theme);
    this.apply(theme);
  }

  private apply(theme: ThemeName): void {
    document.documentElement.setAttribute('data-theme', theme);
  }

  private loadStored(): ThemeName | null {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw === 'light' || raw === 'dark' ? raw : null;
  }
}

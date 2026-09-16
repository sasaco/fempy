import { Injectable } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

export const SELECTED_LANGUAGE = 'lang';

@Injectable({
  providedIn: 'root',
})
export class HelperService {
  constructor(private translate: TranslateService) {}

  /**
   * Get current language from localStorage or browser default
   */
  getLang(): string {
    let savedLang: string | null = null;
    try {
      savedLang = localStorage.getItem(SELECTED_LANGUAGE);
    } catch (e) {
      console.warn('Unable to access localStorage:', e);
    }

    const browserLang = savedLang || this.translate.getBrowserLang() || 'ja';
    return browserLang;
  }

  /**
   * Optional: Change current language
   */
  setLang(lang: string): void {
    this.translate.use(lang);
    try {
      localStorage.setItem(SELECTED_LANGUAGE, lang);
    } catch (e) {
      console.warn('Unable to save lang to localStorage:', e);
    }
  }
}

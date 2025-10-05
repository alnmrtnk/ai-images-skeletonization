import { Injectable } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { BehaviorSubject } from 'rxjs';
import { Language } from '../models/language';

@Injectable({
  providedIn: 'root'
})
export class LanguageService {
  private currentLangSubject = new BehaviorSubject<Language>('uk');
  public currentLang$ = this.currentLangSubject.asObservable();

  constructor(private translate: TranslateService) {
    this.initializeLanguage();
  }

  public initializeLanguage(): void {
    const savedLang = localStorage.getItem('app-language') as Language;
    const defaultLang: Language = savedLang || 'uk';
    
    this.translate.addLangs(['uk', 'en']);
    
    this.setLanguage(defaultLang);
  }

  setLanguage(lang: Language): void {
    this.translate.use(lang);
    this.currentLangSubject.next(lang);
    localStorage.setItem('app-language', lang);
  }

  getCurrentLanguage(): Language {
    return this.currentLangSubject.value;
  }

  switchLanguage(): void {
    const currentLang = this.getCurrentLanguage();
    const newLang: Language = currentLang === 'uk' ? 'en' : 'uk';
    this.setLanguage(newLang);
  }

  getLanguageLabel(lang: Language): string {
    return lang === 'uk' ? 'Українська' : 'English';
  }
}
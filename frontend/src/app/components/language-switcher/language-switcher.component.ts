import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Language, TranslateModule } from '@ngx-translate/core';
import { LanguageService } from '../../services/language.service';

@Component({
  selector: 'app-language-switcher',
  standalone: true,
  imports: [CommonModule, TranslateModule],
  templateUrl: './language-switcher.component.html',
  styleUrls: ['./language-switcher.component.scss']
})
export class LanguageSwitcherComponent {
  private readonly languageService = inject(LanguageService);
  
  currentLang$ = this.languageService.currentLang$;

  switchLanguage(): void {
    this.languageService.switchLanguage();
  }

  getOtherLanguageLabel(currentLang: Language): string {
    return currentLang === 'uk' ? 'English' : 'Українська';
  }
}
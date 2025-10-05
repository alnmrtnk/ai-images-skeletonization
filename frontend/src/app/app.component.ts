import { Component, inject, OnInit } from '@angular/core';
import { LanguageSwitcherComponent } from './components/language-switcher/language-switcher.component';
import { TranslateModule } from '@ngx-translate/core';
import { LanguageService } from './services/language.service';
import { SkeletonComponent } from './components/skeleton/skeleton.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [SkeletonComponent, LanguageSwitcherComponent, TranslateModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit {
  title = 'frontend';
  
  private readonly languageService = inject(LanguageService);

  ngOnInit() {
    this.languageService.initializeLanguage();
  }
}

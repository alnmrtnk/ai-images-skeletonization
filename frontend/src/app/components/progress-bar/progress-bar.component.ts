import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslateModule } from '@ngx-translate/core';

@Component({
  selector: 'app-progress-bar',
  standalone: true,
  imports: [CommonModule, TranslateModule],
  templateUrl: './progress-bar.component.html',
  styleUrls: ['./progress-bar.component.scss']
})
export class ProgressBarComponent {
  @Input() progress = 0;
  @Input() progressText = '';
  @Input() isVisible = false;
}
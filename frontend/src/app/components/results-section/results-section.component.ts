import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SafeUrl } from '@angular/platform-browser';
import { TranslateModule } from '@ngx-translate/core';

@Component({
  selector: 'app-results-section',
  standalone: true,
  imports: [CommonModule, TranslateModule],
  templateUrl: './results-section.component.html',
  styleUrls: ['./results-section.component.scss']
})
export class ResultsSectionComponent {
  @Input() isVisible = false;
  @Input() originalImageUrl: SafeUrl | null = null;
  @Input() processedImageUrl: SafeUrl | null = null;
  
  @Output() downloadRequested = new EventEmitter<string>();

  onDownload(format: string) {
    this.downloadRequested.emit(format);
  }
}
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ImageInfo } from '../../models/image-info';
import { TranslateModule } from '@ngx-translate/core';

@Component({
  selector: 'app-image-info-modal',
  standalone: true,
  imports: [CommonModule, TranslateModule],
  templateUrl: './image-info-modal.component.html',
  styleUrls: ['./image-info-modal.component.scss']
})
export class ImageInfoModalComponent {
  @Input() isVisible = false;
  @Input() imageInfo: ImageInfo | null = null;
  
  @Output() close = new EventEmitter<void>();

  closeModal() {
    this.close.emit();
  }

  formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }
}
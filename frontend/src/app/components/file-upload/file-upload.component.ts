import { Component, EventEmitter, Input, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { ImageInfo } from '../../models/image-info';
import { TranslateModule } from '@ngx-translate/core';

@Component({
  selector: 'app-file-upload',
  standalone: true,
  imports: [CommonModule, TranslateModule],
  templateUrl: './file-upload.component.html',
  styleUrls: ['./file-upload.component.scss']
})
export class FileUploadComponent {
  private readonly sanitizer = inject(DomSanitizer);

  @Input() selectedFile: File | null = null;
  @Input() previewUrl: SafeUrl | null = null;
  @Input() error: string | null = null;
  @Input() imageInfo: ImageInfo | null = null;
  @Input() isProcessing = false;

  @Output() fileSelected = new EventEmitter<File>();
  @Output() fileRemoved = new EventEmitter<void>();
  @Output() uploadRequested = new EventEmitter<void>();
  @Output() resetRequested = new EventEmitter<void>();
  @Output() imageInfoToggle = new EventEmitter<void>();

  isDragOver = false;

  onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files[0]) {
      this.handleFile(input.files[0]);
    }
  }

  onDragOver(event: DragEvent) {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver = true;
  }

  onDragLeave(event: DragEvent) {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver = false;
  }

  onDrop(event: DragEvent) {
    event.preventDefault();
    event.stopPropagation();
    this.isDragOver = false;

    if (event.dataTransfer?.files && event.dataTransfer.files[0]) {
      this.handleFile(event.dataTransfer.files[0]);
    }
  }

  private handleFile(file: File) {
    this.fileSelected.emit(file);
  }

  removeFile(event: Event) {
    event.preventDefault();
    event.stopPropagation();
    this.fileRemoved.emit();
  }

  toggleImageInfo(event: Event) {
    event.preventDefault();
    event.stopPropagation();
    this.imageInfoToggle.emit();
  }

  onUpload() {
    this.uploadRequested.emit();
  }

  onReset() {
    this.resetRequested.emit();
  }

  formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
  }
}
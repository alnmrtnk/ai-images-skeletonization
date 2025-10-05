import { Component, inject } from '@angular/core';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { CommonModule } from '@angular/common';
import { catchError, finalize } from 'rxjs/operators';
import { of } from 'rxjs';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { FileUploadComponent } from '../file-upload/file-upload.component';
import { ProgressBarComponent } from '../progress-bar/progress-bar.component';
import { ImageInfoModalComponent } from '../image-info-modal/image-info-modal.component';
import { ResultsSectionComponent } from '../results-section/results-section.component';
import { SkeletonService } from '../../services/skeleton.service';
import { ImageInfo } from '../../models/image-info';

@Component({
  selector: 'app-skeleton',
  standalone: true,
  templateUrl: './skeleton.component.html',
  styleUrls: ['./skeleton.component.scss'],
  imports: [
    CommonModule,
    FileUploadComponent,
    ProgressBarComponent,
    ImageInfoModalComponent,
    ResultsSectionComponent,
    TranslateModule
  ],
})
export class SkeletonComponent {
  private readonly skeletonService = inject(SkeletonService);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly translate = inject(TranslateService);

  selectedFile: File | null = null;
  processedImage: SafeUrl | null = null;
  previewUrl: SafeUrl | null = null;
  error: string | null = null;
  isProcessing = false;
  progress = 0;
  progressText = '';
  imageInfo: ImageInfo | null = null;
  showImageInfo = false;

  handleFileSelected(file: File) {
    this.error = null;
    
    if (!file.type.startsWith('image/')) {
      this.error = this.translate.instant('ERRORS.INVALID_FILE_TYPE');
      return;
    }

    const maxSize = 10 * 1024 * 1024;
    if (file.size > maxSize) {
      this.error = this.translate.instant('ERRORS.FILE_TOO_LARGE');
      return;
    }

    this.selectedFile = file;
    
    const reader = new FileReader();
    reader.onload = (e) => {
      if (e.target?.result) {
        this.previewUrl = this.sanitizer.bypassSecurityTrustUrl(e.target.result as string);
        this.extractImageInfo(file, e.target.result as string);
      }
    };
    reader.readAsDataURL(file);
  }

  handleFileRemoved() {
    this.selectedFile = null;
    this.previewUrl = null;
    this.error = null;
    this.imageInfo = null;
    this.showImageInfo = false;
  }

  private extractImageInfo(file: File, dataUrl: string) {
    const img = new Image();
    img.onload = () => {
      const extension = file.name.split('.').pop()?.toUpperCase() || this.translate.instant('IMAGE_INFO.UNKNOWN');
      this.imageInfo = {
        name: file.name,
        size: file.size,
        type: file.type,
        width: img.width,
        height: img.height,
        lastModified: new Date(file.lastModified),
        dimensions: `${img.width} × ${img.height} ${this.translate.instant('IMAGE_INFO.PIXELS')}`,
        extension: extension
      };
    };
    img.src = dataUrl;
  }

  toggleImageInfo() {
    this.showImageInfo = !this.showImageInfo;
  }

  closeImageInfo() {
    this.showImageInfo = false;
  }

  onUpload() {
    if (!this.selectedFile) {
      this.error = this.translate.instant('ERRORS.NO_FILE_SELECTED');
      return;
    }

    this.isProcessing = true;
    this.error = null;
    this.progress = 0;
    this.progressText = this.translate.instant('PROGRESS.PREPARING');

    const progressInterval = setInterval(() => {
      if (this.progress < 90) {
        this.progress += Math.random() * 15 + 5;
        if (this.progress < 25) {
          this.progressText = this.translate.instant('PROGRESS.UPLOADING');
        } else if (this.progress < 50) {
          this.progressText = this.translate.instant('PROGRESS.CONVERTING');
        } else if (this.progress < 75) {
          this.progressText = this.translate.instant('PROGRESS.SKELETONIZING');
        } else if (this.progress < 90) {
          this.progressText = this.translate.instant('PROGRESS.ANALYZING');
        }
      }
    }, 300);

    this.skeletonService.uploadImage(this.selectedFile)
      .pipe(
        catchError((error) => {
          console.error('Upload error:', error);
          this.error = this.getErrorMessage(error);
          return of(null);
        }),
        finalize(() => {
          clearInterval(progressInterval);
          this.isProcessing = false;
          this.progress = 100;
          if (!this.error) {
            this.progressText = this.translate.instant('PROGRESS.COMPLETED');
          }
        })
      )
      .subscribe((response) => {
        if (response) {
          const objectURL = URL.createObjectURL(response);
          this.processedImage = this.sanitizer.bypassSecurityTrustUrl(objectURL);
          this.progressText = this.translate.instant('PROGRESS.SUCCESS');
        }
      });
  }

  private getErrorMessage(error: any): string {
    if (error.status === 0) {
      return this.translate.instant('ERRORS.CONNECTION_FAILED');
    } else if (error.status === 400) {
      return this.translate.instant('ERRORS.INVALID_FILE_FORMAT');
    } else if (error.status === 413) {
      return this.translate.instant('ERRORS.FILE_TOO_LARGE_SERVER');
    } else if (error.status === 500) {
      return this.translate.instant('ERRORS.SERVER_ERROR');
    } else {
      return this.translate.instant('ERRORS.UNKNOWN_ERROR');
    }
  }

  downloadResult() {
    if (this.processedImage && this.selectedFile) {
      const url = (this.processedImage as any).changingThisBreaksApplicationSecurity;
      
      const link = document.createElement('a');
      link.href = url;
      link.download = `skeleton_${this.selectedFile.name}`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
    }
  }

  downloadResultInFormat(format: string) {
    if (!this.processedImage || !this.selectedFile) return;

    const url = (this.processedImage as any).changingThisBreaksApplicationSecurity;
    
    const canvas = document.createElement('canvas');
    const ctx = canvas.getContext('2d');
    const img = new Image();
    
    img.onload = () => {
      canvas.width = img.width;
      canvas.height = img.height;
      ctx?.drawImage(img, 0, 0);
      
      let quality = 1.0;
      let mimeType = 'image/png';
      let extension = 'png';
      
      switch (format.toLowerCase()) {
        case 'jpg':
        case 'jpeg':
          mimeType = 'image/jpeg';
          extension = 'jpg';
          quality = 0.9;
          break;
        case 'png':
          mimeType = 'image/png';
          extension = 'png';
          break;
        case 'bmp':
          mimeType = 'image/png';
          extension = 'bmp.png';
          break;
      }
      
      canvas.toBlob((blob) => {
        if (blob) {
          const downloadUrl = URL.createObjectURL(blob);
          const link = document.createElement('a');
          link.href = downloadUrl;
          const baseName = this.selectedFile!.name.replace(/\.[^/.]+$/, '');
          link.download = `skeleton_${baseName}.${extension}`;
          document.body.appendChild(link);
          link.click();
          document.body.removeChild(link);
          URL.revokeObjectURL(downloadUrl);
        }
      }, mimeType, quality);
    };
    
    img.src = url;
  }

  reset() {
    this.selectedFile = null;
    this.processedImage = null;
    this.previewUrl = null;
    this.error = null;
    this.isProcessing = false;
    this.progress = 0;
    this.progressText = '';
    this.imageInfo = null;
    this.showImageInfo = false;
  }
}

import { Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { Observable, throwError, timeout } from 'rxjs';
import { catchError, retry } from 'rxjs/operators';
import { environment } from '../../environments/environment';

@Injectable({ providedIn: 'root' })
export class SkeletonService {
  private readonly apiUrl = `${environment.apiUrl}/skeletonize`;
  private readonly requestTimeout = 120000;
  private readonly maxRetries = 2;

  constructor(private http: HttpClient) {}

  uploadImage(file: File): Observable<Blob> {
    if (!this.isValidImageFile(file)) {
      return throwError(() => new Error('Invalid image file format'));
    }

    if (file.size > 10 * 1024 * 1024) {
      return throwError(() => new Error('File size exceeds 10MB limit'));
    }

    const formData = new FormData();
    formData.append('file', file);

    const headers = new HttpHeaders({
      'Accept': 'image/*,application/octet-stream'
    });

    return this.http.post(this.apiUrl, formData, { 
      headers,
      responseType: 'blob',
      reportProgress: true,
      observe: 'body'
    }).pipe(
      timeout(this.requestTimeout),
      retry(this.maxRetries),
      catchError(this.handleError.bind(this))
    );
  }

  private isValidImageFile(file: File): boolean {
    const validTypes = [
      'image/jpeg',
      'image/jpg', 
      'image/png',
      'image/bmp',
      'image/gif',
      'image/webp'
    ];
    return validTypes.includes(file.type.toLowerCase());
  }

  private handleError(error: HttpErrorResponse): Observable<never> {
    let errorMessage = 'An unknown error occurred';

    if (error.error instanceof ErrorEvent) {
      errorMessage = `Client Error: ${error.error.message}`;
    } else {
      switch (error.status) {
        case 0:
          errorMessage = 'Unable to connect to server. Please check if the backend is running.';
          break;
        case 400:
          errorMessage = 'Bad request. Please check your image file format.';
          break;
        case 413:
          errorMessage = 'File too large. Maximum size is 10MB.';
          break;
        case 415:
          errorMessage = 'Unsupported media type. Please use JPEG, PNG, or BMP format.';
          break;
        case 422:
          errorMessage = 'Invalid image data. Please try a different image.';
          break;
        case 500:
          errorMessage = 'Server error occurred while processing the image.';
          break;
        case 503:
          errorMessage = 'Service temporarily unavailable. Please try again later.';
          break;
        case 504:
          errorMessage = 'Request timeout. The image processing took too long.';
          break;
        default:
          errorMessage = `Server Error: ${error.status} - ${error.message}`;
      }
    }

    console.error('SkeletonService Error:', error);
    return throwError(() => ({ ...error, message: errorMessage }));
  }

  checkServiceHealth(): Observable<any> {
    return this.http.get(`${this.apiUrl.replace('/skeletonize', '/health')}`).pipe(
      timeout(5000),
      catchError(() => throwError(() => new Error('Service is not available')))
    );
  }
}

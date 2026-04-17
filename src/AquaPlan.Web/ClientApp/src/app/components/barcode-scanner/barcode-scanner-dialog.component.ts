import { ChangeDetectionStrategy, Component, ElementRef, OnDestroy, ViewChild, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { TranslateModule } from '@ngx-translate/core';
import { Subscription } from 'rxjs';
import { BarcodeScannerService } from '../../services/barcode-scanner.service';

export interface BarcodeScanResult {
  barcode: string;
}

@Component({
  selector: 'app-barcode-scanner-dialog',
  standalone: true,
  imports: [MatDialogModule, MatButtonModule, MatIconModule, TranslateModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h2 mat-dialog-title>{{ 'scan.title' | translate }}</h2>
    <mat-dialog-content class="scanner-content">
      @if (error()) {
        <div class="scanner-error">
          <mat-icon>videocam_off</mat-icon>
          <p>{{ 'scan.permissionDenied' | translate }}</p>
        </div>
      } @else {
        <p class="scanner-instruction">{{ 'scan.instruction' | translate }}</p>
        <div class="scanner-wrapper">
          <video #video autoplay playsinline muted class="scanner-video"></video>
          <div class="scanner-overlay"></div>
        </div>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="cancel()">{{ 'scan.cancel' | translate }}</button>
    </mat-dialog-actions>
  `,
  styles: [`
    .scanner-content { min-width: 320px; max-width: 480px; padding: 0 16px 16px; }
    .scanner-instruction { margin: 0 0 12px; color: rgba(0,0,0,0.7); font-size: 13px; }
    .scanner-wrapper { position: relative; width: 100%; aspect-ratio: 4/3; background: #000; border-radius: 8px; overflow: hidden; }
    .scanner-video { width: 100%; height: 100%; object-fit: cover; }
    .scanner-overlay {
      position: absolute;
      inset: 15% 10%;
      border: 2px solid rgba(255,255,255,0.75);
      border-radius: 6px;
      box-shadow: 0 0 0 9999px rgba(0,0,0,0.25);
      pointer-events: none;
    }
    .scanner-error { display: flex; flex-direction: column; align-items: center; gap: 8px; padding: 32px 16px; color: #C62828; }
    .scanner-error mat-icon { font-size: 48px; width: 48px; height: 48px; }
  `],
})
export class BarcodeScannerDialogComponent implements OnDestroy {
  @ViewChild('video', { static: false }) private readonly videoRef?: ElementRef<HTMLVideoElement>;

  private readonly dialogRef = inject(MatDialogRef<BarcodeScannerDialogComponent, BarcodeScanResult | null>);
  private readonly scanner = inject(BarcodeScannerService);
  private stream: MediaStream | null = null;
  private subscription: Subscription | null = null;

  readonly error = signal(false);

  ngAfterViewInit(): void {
    void this.startCamera();
  }

  ngOnDestroy(): void {
    this.stop();
  }

  cancel(): void {
    this.stop();
    this.dialogRef.close(null);
  }

  private async startCamera(): Promise<void> {
    if (!this.videoRef) {
      return;
    }
    try {
      this.stream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: { ideal: 'environment' } },
        audio: false,
      });
      const video = this.videoRef.nativeElement;
      video.srcObject = this.stream;
      await video.play();
      this.subscription = this.scanner.startScan(video).subscribe({
        next: (barcode) => {
          this.stop();
          this.dialogRef.close({ barcode });
        },
        error: () => {
          this.error.set(true);
          this.stop();
        },
      });
    } catch {
      this.error.set(true);
    }
  }

  private stop(): void {
    this.subscription?.unsubscribe();
    this.subscription = null;
    if (this.stream) {
      for (const track of this.stream.getTracks()) {
        track.stop();
      }
      this.stream = null;
    }
  }
}

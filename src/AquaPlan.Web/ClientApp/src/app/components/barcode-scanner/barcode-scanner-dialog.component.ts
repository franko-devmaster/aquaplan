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
          <p>{{ errorKey() | translate }}</p>
        </div>
      } @else {
        <p class="scanner-instruction">{{ 'scan.instruction' | translate }}</p>
        <div class="scanner-wrapper">
          <video #video autoplay playsinline muted class="scanner-video"
                 webkit-playsinline></video>
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
  /** AQ-402 — pick a translated label tailored to the failure mode (HTTPS vs. permission vs. hardware). */
  readonly errorKey = signal<string>('scan.permissionDenied');

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
    // AQ-402 — getUserMedia requires a secure context. Fail early with an
    // actionable message instead of a generic permission error.
    if (typeof window !== 'undefined'
        && window.isSecureContext === false
        && window.location.hostname !== 'localhost'
        && window.location.hostname !== '127.0.0.1') {
      this.errorKey.set('scan.httpsRequired');
      this.error.set(true);
      return;
    }
    if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
      this.errorKey.set('scan.unsupported');
      this.error.set(true);
      return;
    }

    // AQ-404 — Safari iOS is picky about autoplay. Explicitly set the
    // properties on the element (the template attributes alone are sometimes
    // not enough when the element has just been attached).
    const video = this.videoRef.nativeElement;
    video.setAttribute('playsinline', 'true');
    video.setAttribute('webkit-playsinline', 'true');
    video.setAttribute('muted', 'true');
    video.muted = true;
    video.playsInline = true;

    // AQ-404 — use constraints that Safari iOS accepts. `facingMode: environment`
    // as a plain string can throw OverconstrainedError on front-camera-only
    // iPhones; the `ideal` variant lets Safari fall back gracefully.
    const constraints: MediaStreamConstraints = {
      video: { facingMode: { ideal: 'environment' } },
      audio: false,
    };

    try {
      this.stream = await this.acquireStream(constraints);
      video.srcObject = this.stream;
      // AQ-404 — Safari iOS sometimes requires a small wait before play()
      // resolves; wrap in try/catch and keep going even on rejection.
      try {
        await video.play();
      } catch (playErr) {
        // eslint-disable-next-line no-console
        console.warn('[scanner] video.play() rejected:', playErr);
      }
      this.subscription = this.scanner.startScan(video).subscribe({
        next: (barcode) => {
          this.stop();
          this.dialogRef.close({ barcode });
        },
        error: (err: unknown) => {
          // eslint-disable-next-line no-console
          console.error('[scanner] decode pipeline error:', err);
          this.errorKey.set('scan.permissionDenied');
          this.error.set(true);
          this.stop();
        },
      });
    } catch (err: unknown) {
      // AQ-404 — log the actual failure so it can be diagnosed on iPhone via
      // Safari remote debug (no way to see a silent catch otherwise).
      // eslint-disable-next-line no-console
      console.error('[scanner] getUserMedia failed:', err);
      // AQ-402 — differentiate denial vs. missing device vs. other.
      const name = (err as { name?: string } | null)?.name;
      if (name === 'NotAllowedError' || name === 'SecurityError') {
        this.errorKey.set('scan.permissionDenied');
      } else if (name === 'NotFoundError' || name === 'OverconstrainedError') {
        this.errorKey.set('scan.noCamera');
      } else {
        this.errorKey.set('scan.permissionDenied');
      }
      this.error.set(true);
    }
  }

  /**
   * AQ-404 — Try the preferred constraints first, then fall back to the most
   * permissive (`video: true`) so Safari iOS still gets a stream even when it
   * rejects `facingMode` (seen on some older iPads / front-camera contexts).
   */
  private async acquireStream(constraints: MediaStreamConstraints): Promise<MediaStream> {
    try {
      return await navigator.mediaDevices.getUserMedia(constraints);
    } catch (err: unknown) {
      const name = (err as { name?: string } | null)?.name;
      if (name === 'OverconstrainedError' || name === 'NotReadableError') {
        // eslint-disable-next-line no-console
        console.warn('[scanner] retrying with permissive constraints after', name);
        return await navigator.mediaDevices.getUserMedia({ video: true, audio: false });
      }
      throw err;
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

import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';

/**
 * BarcodeDetector is still an experimental Web API. Declare the subset we need
 * so TypeScript compiles without DOM typings for it.
 */
interface BarcodeDetectorLike {
  detect(source: HTMLVideoElement | ImageBitmap): Promise<{ rawValue: string }[]>;
}
type BarcodeDetectorConstructor = new (options?: { formats?: string[] }) => BarcodeDetectorLike;

@Injectable({ providedIn: 'root' })
export class BarcodeScannerService {
  /** Returns true when the native `BarcodeDetector` API is available. */
  isNativeSupported(): boolean {
    // AQ-402 — Safari on iOS advertises no BarcodeDetector anyway, but even
    // desktop Safari sometimes exposes a partial implementation that throws
    // on detect(). Feature-detect + exclude WebKit/iOS to stay on ZXing there.
    if (typeof window === 'undefined') return false;
    if (!('BarcodeDetector' in (window as unknown as Record<string, unknown>))) {
      return false;
    }
    if (this.isIosSafari()) {
      return false;
    }
    return true;
  }

  /** AQ-402 — rough UA sniff used to bypass the (missing) native API on iOS. */
  private isIosSafari(): boolean {
    if (typeof navigator === 'undefined') return false;
    const ua = navigator.userAgent ?? '';
    const isIos = /iPad|iPhone|iPod/.test(ua) ||
      (ua.includes('Mac') && typeof document !== 'undefined' && 'ontouchend' in document);
    const isSafari = /Safari/.test(ua) && !/CriOS|FxiOS|EdgiOS/.test(ua);
    return isIos && isSafari;
  }

  /**
   * Start scanning the given <video> element. Prefers the native `BarcodeDetector`
   * when available and falls back to `@zxing/browser`. Emits the decoded
   * barcode string once and completes. Unsubscribe/complete the observable to
   * stop the scan (it also releases the ZXing reader).
   */
  startScan(videoElement: HTMLVideoElement): Observable<string> {
    if (this.isNativeSupported()) {
      return this.scanWithNative(videoElement);
    }
    return this.scanWithZxing(videoElement);
  }

  private scanWithNative(videoElement: HTMLVideoElement): Observable<string> {
    return new Observable<string>((subscriber) => {
      const BarcodeDetectorCtor = (window as unknown as Record<string, unknown>)['BarcodeDetector'] as BarcodeDetectorConstructor;
      const detector = new BarcodeDetectorCtor({
        formats: ['code_128', 'code_39', 'ean_13', 'ean_8', 'qr_code', 'data_matrix', 'upc_a', 'upc_e'],
      });
      let cancelled = false;
      const tick = async (): Promise<void> => {
        if (cancelled) {
          return;
        }
        try {
          if (videoElement.readyState >= 2) {
            const detections = await detector.detect(videoElement);
            if (detections.length > 0 && detections[0].rawValue) {
              subscriber.next(detections[0].rawValue);
              subscriber.complete();
              return;
            }
          }
        } catch {
          // swallow transient detection errors; keep polling
        }
        if (!cancelled) {
          requestAnimationFrame(() => {
            void tick();
          });
        }
      };
      void tick();
      return () => {
        cancelled = true;
      };
    });
  }

  private scanWithZxing(videoElement: HTMLVideoElement): Observable<string> {
    return new Observable<string>((subscriber) => {
      let controls: { stop: () => void } | null = null;
      // Dynamic import keeps zxing out of the initial bundle
      import('@zxing/browser')
        .then(({ BrowserMultiFormatReader }) => {
          const reader = new BrowserMultiFormatReader();
          return reader.decodeFromVideoDevice(undefined, videoElement, (result, _err, ctrls) => {
            controls = ctrls;
            if (result) {
              subscriber.next(result.getText());
              subscriber.complete();
              ctrls.stop();
            }
          });
        })
        .catch((err: unknown) => {
          subscriber.error(err);
        });
      return () => {
        controls?.stop();
      };
    });
  }
}

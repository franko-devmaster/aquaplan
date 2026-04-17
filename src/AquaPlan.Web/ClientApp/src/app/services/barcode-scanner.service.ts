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
    return typeof window !== 'undefined' && 'BarcodeDetector' in (window as unknown as Record<string, unknown>);
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

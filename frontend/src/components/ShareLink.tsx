import { useState } from 'react';
import { Copy, Check, QrCode } from 'lucide-react';
import { QRCodeSVG } from 'qrcode.react';
import { useToast } from './Toast';

interface ShareLinkProps {
  code: string;
}

export function ShareLink({ code }: ShareLinkProps) {
  const [copied, setCopied] = useState(false);
  const [showQr, setShowQr] = useState(false);
  const { toast } = useToast();
  const url = `${window.location.origin}/poll/${code}`;

  const copy = async () => {
    await navigator.clipboard.writeText(url);
    setCopied(true);
    toast('Link copied');
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div className="share-link-wrap">
      <div className="share-link">
        <code className="share-link__url">{url}</code>
        <button
          onClick={copy}
          className="btn-outline share-link__copy"
          aria-label={copied ? 'Link copied' : 'Copy share link'}
        >
          {copied ? (
            <>
              <Check size={16} strokeWidth={2.25} aria-hidden="true" /> Copied
            </>
          ) : (
            <>
              <Copy size={16} strokeWidth={2.25} aria-hidden="true" /> Copy
            </>
          )}
        </button>
        <button
          type="button"
          onClick={() => setShowQr((v) => !v)}
          className="btn-outline share-link__qr-toggle"
          aria-expanded={showQr}
          aria-label={showQr ? 'Hide QR code' : 'Show QR code'}
        >
          <QrCode size={16} strokeWidth={2.25} aria-hidden="true" /> {showQr ? 'Hide QR' : 'Show QR'}
        </button>
      </div>
      {showQr && (
        <div className="share-link__qr">
          <QRCodeSVG value={url} size={180} aria-label="QR code for this poll" />
          <p className="muted">Scan to open this poll</p>
        </div>
      )}
    </div>
  );
}

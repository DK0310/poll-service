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
    <div className="flex flex-col gap-4">
      <div className="flex max-w-full items-center gap-2">
        <code className="min-w-0 flex-1 overflow-x-auto whitespace-nowrap rounded-lg border border-line bg-bg px-3.5 py-2.5 text-left font-mono text-sm text-fg">
          {url}
        </code>
        <button
          onClick={copy}
          className="board-btn-outline flex-none whitespace-nowrap !px-4"
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
          className="board-btn-outline flex-none whitespace-nowrap !px-4"
          aria-expanded={showQr}
          aria-label={showQr ? 'Hide QR code' : 'Show QR code'}
        >
          <QrCode size={16} strokeWidth={2.25} aria-hidden="true" /> {showQr ? 'Hide QR' : 'Show QR'}
        </button>
      </div>
      {showQr && (
        // QR stays on a white quiet-zone for reliable scanning, even in the dark theme.
        <div className="mx-auto flex flex-col items-center gap-2 rounded-xl border border-line bg-white p-4">
          <QRCodeSVG value={url} size={180} className="block h-auto" aria-label="QR code for this poll" />
          <p className="text-sm text-[#6f6880]">Scan to open this poll</p>
        </div>
      )}
    </div>
  );
}

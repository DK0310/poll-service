import { useState } from 'react';
import { Copy, Check } from 'lucide-react';

interface ShareLinkProps {
  code: string;
}

export function ShareLink({ code }: ShareLinkProps) {
  const [copied, setCopied] = useState(false);
  const url = `${window.location.origin}/poll/${code}`;

  const copy = async () => {
    await navigator.clipboard.writeText(url);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
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
    </div>
  );
}

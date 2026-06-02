import { useState } from 'react';

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
      <code>{url}</code>
      <button onClick={copy} className="btn-copy">
        {copied ? '✓ Copied!' : 'Copy Link'}
      </button>
    </div>
  );
}

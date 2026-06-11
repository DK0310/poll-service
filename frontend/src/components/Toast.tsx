import { createContext, useCallback, useContext, useRef, useState, type ReactNode } from 'react';
import { CheckCircle2, AlertCircle, X } from 'lucide-react';

type ToastKind = 'success' | 'error';

interface ToastItem {
  id: number;
  message: string;
  kind: ToastKind;
}

interface ToastApi {
  toast: (message: string, kind?: ToastKind) => void;
}

const ToastContext = createContext<ToastApi | null>(null);

// eslint-disable-next-line react-refresh/only-export-components
export function useToast(): ToastApi {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error('useToast must be used within a ToastProvider');
  return ctx;
}

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<ToastItem[]>([]);
  const nextId = useRef(0);

  const dismiss = useCallback((id: number) => {
    setToasts((list) => list.filter((t) => t.id !== id));
  }, []);

  const toast = useCallback(
    (message: string, kind: ToastKind = 'success') => {
      const id = ++nextId.current;
      setToasts((list) => [...list, { id, message, kind }]);
      setTimeout(() => dismiss(id), 3000);
    },
    [dismiss],
  );

  return (
    <ToastContext.Provider value={{ toast }}>
      {children}
      <div className="toast-viewport" role="status" aria-live="polite">
        {toasts.map((t) => (
          <button
            key={t.id}
            type="button"
            className={`toast toast--${t.kind}`}
            onClick={() => dismiss(t.id)}
            aria-label="Dismiss notification"
          >
            {t.kind === 'success' ? (
              <CheckCircle2 size={18} strokeWidth={2.25} aria-hidden="true" />
            ) : (
              <AlertCircle size={18} strokeWidth={2.25} aria-hidden="true" />
            )}
            <span className="toast__msg">{t.message}</span>
            <X size={14} strokeWidth={2.5} aria-hidden="true" className="toast__x" />
          </button>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

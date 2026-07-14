import { useRef, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import {
  Camera, Loader2, Mail, ShieldCheck, KeyRound, Vote as VoteIcon,
  ArrowUpRight, CalendarDays, Trash2,
} from 'lucide-react';
import { useProfile } from '../hooks/useProfile';
import { useVoteHistory } from '../hooks/useVoteHistory';
import { useToast } from '../components/Toast';
import { fileToAvatarDataUrl } from '../utils/avatar';
import type { Profile } from '../types/poll.types';

export function ProfilePage() {
  const { profile, loading, error, save, requestChangeCode, changePassword, setError } = useProfile();

  if (loading) return <ProfileSkeleton />;
  if (!profile) {
    return (
      <div className="board mx-auto w-full max-w-md">
        <div className="board-panel p-8 text-center text-fg-muted">
          {error ?? 'Could not load your profile.'}
        </div>
      </div>
    );
  }

  return (
    <div className="board mx-auto w-full max-w-4xl">
      <h1 className="mb-6 font-display text-2xl font-bold tracking-tight text-fg sm:text-3xl">
        Your profile
      </h1>
      <div className="grid gap-6 lg:grid-cols-[300px_1fr]">
        <IdentityCard profile={profile} onAvatar={(avatarUrl) => save({ avatarUrl })} />
        <div className="flex min-w-0 flex-col gap-6">
          <ProfileDetails profile={profile} onSave={save} />
          <SecuritySection
            profile={profile}
            onRequestCode={requestChangeCode}
            onChangePassword={changePassword}
            setError={setError}
          />
          <VoteHistorySection />
        </div>
      </div>
    </div>
  );
}

// ── Identity card (left rail) ─────────────────────────────────
function IdentityCard({ profile, onAvatar }: { profile: Profile; onAvatar: (dataUrl: string) => Promise<boolean> }) {
  const { toast } = useToast();
  const fileRef = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);
  const [avatarError, setAvatarError] = useState<string | null>(null);

  const displayName = profile.username?.trim() || profile.email.split('@')[0];

  const pick = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    e.target.value = ''; // allow re-picking the same file
    if (!file) return;
    setAvatarError(null);
    setBusy(true);
    try {
      const dataUrl = await fileToAvatarDataUrl(file);
      if (await onAvatar(dataUrl)) toast('Photo updated.');
    } catch (err) {
      setAvatarError(err instanceof Error ? err.message : 'Could not use that image.');
    } finally {
      setBusy(false);
    }
  };

  const removePhoto = async () => {
    setBusy(true);
    if (await onAvatar('')) toast('Photo removed.');
    setBusy(false);
  };

  return (
    <aside className="board-panel h-fit p-6 lg:sticky lg:top-24">
      <div className="flex flex-col items-center text-center">
        <div className="relative">
          <button
            type="button"
            onClick={() => fileRef.current?.click()}
            disabled={busy}
            className="group relative grid h-28 w-28 place-items-center overflow-hidden rounded-full ring-2 ring-line transition-shadow hover:ring-tangerine focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-tangerine"
            aria-label="Change profile photo"
          >
            <Avatar profile={profile} className="h-full w-full" />
            <span className="absolute inset-0 grid place-items-center bg-bg/60 opacity-0 transition-opacity group-hover:opacity-100">
              {busy ? (
                <Loader2 size={22} className="board-spin text-fg" aria-hidden="true" />
              ) : (
                <Camera size={22} strokeWidth={2.25} className="text-fg" aria-hidden="true" />
              )}
            </span>
          </button>
          <input ref={fileRef} type="file" accept="image/*" hidden onChange={pick} />
        </div>

        <h2 className="mt-4 max-w-full truncate font-display text-xl font-bold text-fg" title={displayName}>
          {displayName}
        </h2>

        <div className="mt-1 flex items-center gap-1.5 text-sm text-fg-muted">
          <Mail size={14} strokeWidth={2.25} aria-hidden="true" />
          <span className="max-w-[200px] truncate" title={profile.email}>{profile.email}</span>
        </div>

        <div className="mt-3 flex flex-wrap items-center justify-center gap-2">
          <span className="inline-flex items-center gap-1 rounded-full border border-line bg-panel-2 px-2.5 py-1 text-xs font-semibold text-fg-muted">
            {profile.hasGoogle ? 'Google' : 'Password'} sign-in
          </span>
          {profile.role === 'Admin' && (
            <span className="inline-flex items-center gap-1 rounded-full bg-tangerine px-2.5 py-1 text-xs font-semibold text-on-accent">
              <ShieldCheck size={12} strokeWidth={2.5} aria-hidden="true" /> Admin
            </span>
          )}
        </div>

        {profile.avatarUrl && (
          <button
            type="button"
            onClick={removePhoto}
            disabled={busy}
            className="mt-4 inline-flex items-center gap-1.5 text-xs font-semibold text-fg-muted transition-colors hover:text-danger disabled:opacity-50"
          >
            <Trash2 size={13} strokeWidth={2.25} aria-hidden="true" /> Remove photo
          </button>
        )}

        {avatarError && <p className="mt-3 text-xs text-danger" role="alert">{avatarError}</p>}

        <div className="mt-5 flex items-center gap-1.5 border-t border-line pt-4 font-mono text-xs text-fg-faint">
          <CalendarDays size={13} strokeWidth={2.25} aria-hidden="true" />
          Joined {new Date(profile.createdAt).toLocaleDateString(undefined, { month: 'short', year: 'numeric' })}
        </div>
      </div>
    </aside>
  );
}

// Avatar image, or an initials disc when none is set.
function Avatar({ profile, className }: { profile: Profile; className?: string }) {
  if (profile.avatarUrl) {
    return <img src={profile.avatarUrl} alt="" className={`object-cover ${className ?? ''}`} />;
  }
  const seed = profile.username?.trim() || profile.email;
  const initials = seed.slice(0, 2).toUpperCase();
  return (
    <span className={`grid place-items-center bg-panel-2 font-display text-2xl font-bold text-tangerine ${className ?? ''}`}>
      {initials}
    </span>
  );
}

// ── Profile details (username + bio) ──────────────────────────
function ProfileDetails({ profile, onSave }: { profile: Profile; onSave: (u: { username: string; bio: string }) => Promise<boolean> }) {
  const { toast } = useToast();
  const [username, setUsername] = useState(profile.username ?? '');
  const [bio, setBio] = useState(profile.bio ?? '');
  const [saving, setSaving] = useState(false);

  const dirty = username !== (profile.username ?? '') || bio !== (profile.bio ?? '');

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setSaving(true);
    if (await onSave({ username, bio })) toast('Profile saved.');
    setSaving(false);
  };

  return (
    <section className="board-panel p-6 sm:p-7">
      <h3 className="mb-5 font-display text-lg font-bold text-fg">Profile details</h3>
      <form onSubmit={submit} className="flex flex-col gap-4">
        <div>
          <label htmlFor="username" className="board-label">Username</label>
          <input
            id="username"
            className="board-input"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            maxLength={50}
            placeholder={profile.email.split('@')[0]}
            disabled={saving}
          />
        </div>
        <div>
          <label htmlFor="bio" className="board-label">Bio</label>
          <textarea
            id="bio"
            className="board-input min-h-[88px] resize-y"
            value={bio}
            onChange={(e) => setBio(e.target.value)}
            maxLength={300}
            placeholder="A sentence about you."
            disabled={saving}
          />
          <p className="mt-1 text-right font-mono text-xs text-fg-faint">{bio.length}/300</p>
        </div>
        <div>
          <label className="board-label">Email</label>
          <input className="board-input opacity-60" value={profile.email} readOnly disabled />
          <p className="mt-1 text-xs text-fg-faint">Email can’t be changed.</p>
        </div>
        <div className="flex justify-end">
          <button type="submit" className="board-btn" disabled={!dirty || saving}>
            {saving && <Loader2 size={18} className="board-spin" aria-hidden="true" />}
            {saving ? 'Saving…' : 'Save changes'}
          </button>
        </div>
      </form>
    </section>
  );
}

// ── Security (set / change password) ──────────────────────────
function SecuritySection({
  profile, onRequestCode, onChangePassword, setError,
}: {
  profile: Profile;
  onRequestCode: () => Promise<boolean>;
  onChangePassword: (current: string, next: string, code: string) => Promise<boolean>;
  setError: (e: string | null) => void;
}) {
  const { toast } = useToast();
  const [current, setCurrent] = useState('');
  const [next, setNext] = useState('');
  const [confirm, setConfirm] = useState('');
  const [code, setCode] = useState('');
  const [saving, setSaving] = useState(false);
  const [sending, setSending] = useState(false);
  const [codeSent, setCodeSent] = useState(false);
  const [localError, setLocalError] = useState<string | null>(null);

  const isSet = !profile.hasPassword; // first-time set (Google) vs change

  // A real change needs an emailed OTP; a first-time set does not.
  const sendCode = async () => {
    setLocalError(null);
    setError(null);
    setSending(true);
    const ok = await onRequestCode();
    setSending(false);
    if (ok) {
      setCodeSent(true);
      toast(`Verification code sent to ${profile.email}.`);
    } else {
      setLocalError('Could not send the code. Please try again.');
    }
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setLocalError(null);
    setError(null);
    if (next.length < 6) return setLocalError('New password must be at least 6 characters.');
    if (next !== confirm) return setLocalError('Passwords don’t match.');
    if (!isSet && code.length < 6) return setLocalError('Enter the 6-digit code we emailed you.');

    setSaving(true);
    const ok = await onChangePassword(isSet ? '' : current, next, isSet ? '' : code);
    setSaving(false);
    if (ok) {
      toast(isSet ? 'Password set.' : 'Password changed.');
      setCurrent(''); setNext(''); setConfirm(''); setCode(''); setCodeSent(false);
    } else {
      setLocalError(
        isSet
          ? 'Could not set your password.'
          : 'Could not change your password. Check your current password and code.',
      );
    }
  };

  return (
    <section className="board-panel p-6 sm:p-7">
      <div className="mb-1 flex items-center gap-2">
        <KeyRound size={18} strokeWidth={2.25} className="text-tangerine" aria-hidden="true" />
        <h3 className="font-display text-lg font-bold text-fg">{isSet ? 'Set a password' : 'Change password'}</h3>
      </div>
      <p className="mb-5 text-sm text-fg-muted">
        {isSet
          ? 'You sign in with Google. Add a password to also sign in with email.'
          : 'Enter your current password, verify with the code we email you, then choose a new one.'}
      </p>
      <form onSubmit={submit} className="flex flex-col gap-4">
        {!isSet && (
          <div>
            <label htmlFor="current" className="board-label">Current password</label>
            <input
              id="current" type="password" className="board-input" autoComplete="current-password"
              value={current} onChange={(e) => setCurrent(e.target.value)} disabled={saving} required
            />
          </div>
        )}
        {!isSet && (
          <div>
            <label htmlFor="code" className="board-label">Verification code</label>
            <div className="flex gap-2">
              <input
                id="code" inputMode="numeric" autoComplete="one-time-code"
                className="board-input tracking-[0.4em]"
                value={code}
                onChange={(e) => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
                maxLength={6} placeholder="000000" disabled={saving || !codeSent}
              />
              <button
                type="button" className="board-btn-outline shrink-0"
                onClick={sendCode} disabled={sending || saving}
              >
                {sending && <Loader2 size={16} className="board-spin" aria-hidden="true" />}
                {sending ? 'Sending…' : codeSent ? 'Resend code' : 'Send code'}
              </button>
            </div>
            {codeSent && (
              <p className="mt-1 text-xs text-fg-faint">
                Code sent to {profile.email}. It expires in 10 minutes. Can’t find it? Check your spam or junk folder.
              </p>
            )}
          </div>
        )}
        <div>
          <label htmlFor="next" className="board-label">
            New password <span className="font-body normal-case tracking-normal text-fg-faint">(min 6)</span>
          </label>
          <input
            id="next" type="password" className="board-input" autoComplete="new-password"
            value={next} onChange={(e) => setNext(e.target.value)} minLength={6} disabled={saving} required
          />
        </div>
        <div>
          <label htmlFor="confirm" className="board-label">Confirm new password</label>
          <input
            id="confirm" type="password" className="board-input" autoComplete="new-password"
            value={confirm} onChange={(e) => setConfirm(e.target.value)} minLength={6} disabled={saving} required
          />
        </div>
        {localError && <p className="text-sm text-danger" role="alert">{localError}</p>}
        <div className="flex justify-end">
          <button type="submit" className="board-btn" disabled={saving || (!isSet && !codeSent)}>
            {saving && <Loader2 size={18} className="board-spin" aria-hidden="true" />}
            {saving ? 'Saving…' : isSet ? 'Set password' : 'Change password'}
          </button>
        </div>
      </form>
    </section>
  );
}

// ── Vote history ──────────────────────────────────────────────
function VoteHistorySection() {
  const { items, error } = useVoteHistory();

  return (
    <section className="board-panel p-6 sm:p-7">
      <div className="mb-5 flex items-center gap-2">
        <VoteIcon size={18} strokeWidth={2.25} className="text-tangerine" aria-hidden="true" />
        <h3 className="font-display text-lg font-bold text-fg">Vote history</h3>
        {items && items.length > 0 && (
          <span className="ml-auto font-mono text-xs text-fg-faint">{items.length} poll{items.length === 1 ? '' : 's'}</span>
        )}
      </div>

      {items === null && !error && (
        <ul className="flex flex-col gap-2">
          {[0, 1, 2].map((i) => (
            <li key={i} className="h-14 animate-pulse rounded-xl border border-line bg-panel-2" />
          ))}
        </ul>
      )}

      {items && items.length === 0 && (
        <div className="rounded-xl border border-dashed border-line px-4 py-8 text-center">
          <p className="font-display font-semibold text-fg">No votes yet</p>
          <p className="mt-1 text-sm text-fg-muted">
            Join a poll from its link or QR code — the polls you answer will appear here.
          </p>
        </div>
      )}

      {items && items.length > 0 && (
        <ul className="flex flex-col gap-2">
          {items.map((it) => (
            <li key={it.pollCode}>
              <Link
                to={`/poll/${it.pollCode}/results`}
                className="group flex items-center gap-3 rounded-xl border border-line bg-panel-2 px-4 py-3 transition-colors hover:border-fg-muted"
              >
                <span className="grid h-9 w-9 shrink-0 place-items-center rounded-lg bg-panel font-mono text-xs font-semibold text-tangerine">
                  {it.pollCode}
                </span>
                <span className="min-w-0 flex-1">
                  <span className="block truncate font-display font-semibold text-fg">
                    {it.title || 'Untitled poll'}
                  </span>
                  <span className="text-xs text-fg-muted">
                    {it.answerCount} answer{it.answerCount === 1 ? '' : 's'} · {timeAgo(it.votedAt)}
                    {' · '}
                    <span className={it.isActive ? 'text-teal' : 'text-fg-faint'}>
                      {it.isActive ? 'Live' : 'Closed'}
                    </span>
                  </span>
                </span>
                <ArrowUpRight
                  size={16} strokeWidth={2.25}
                  className="shrink-0 text-fg-faint transition-colors group-hover:text-fg"
                  aria-hidden="true"
                />
              </Link>
            </li>
          ))}
        </ul>
      )}

      {error && items?.length === 0 && (
        <p className="mt-2 text-sm text-danger" role="alert">{error}</p>
      )}
    </section>
  );
}

// ── helpers ───────────────────────────────────────────────────
function timeAgo(iso: string): string {
  const secs = Math.floor((Date.now() - new Date(iso).getTime()) / 1000);
  const units: [number, string][] = [
    [60, 'sec'], [60, 'min'], [24, 'hr'], [7, 'day'], [4.35, 'wk'], [12, 'mo'], [Infinity, 'yr'],
  ];
  let value = secs;
  let unit = 'sec';
  for (const [div, name] of units) {
    if (value < div) { unit = name; break; }
    value = Math.floor(value / div);
    unit = name;
  }
  if (unit === 'sec' && value < 30) return 'just now';
  return `${value} ${unit}${value === 1 ? '' : 's'} ago`;
}

function ProfileSkeleton() {
  return (
    <div className="board mx-auto w-full max-w-4xl">
      <div className="mb-6 h-8 w-48 animate-pulse rounded-lg bg-panel-2" />
      <div className="grid gap-6 lg:grid-cols-[300px_1fr]">
        <div className="board-panel h-72 animate-pulse" />
        <div className="flex flex-col gap-6">
          <div className="board-panel h-64 animate-pulse" />
          <div className="board-panel h-56 animate-pulse" />
        </div>
      </div>
    </div>
  );
}

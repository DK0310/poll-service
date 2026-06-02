import { useEffect, useRef, useState } from 'react';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import axios from 'axios';
import api from '../api/api';
import type { VoteResults } from '../types/poll.types';

const HUB_URL = import.meta.env.VITE_HUB_URL ?? 'http://localhost:5000/hubs/poll';

/**
 * Live results via SignalR (replaces Phase 4's usePolledResults):
 *   1. fetch the initial snapshot over REST (through the Gateway)
 *   2. open a WebSocket to the hub (proxied by the Gateway to the Vote API)
 *   3. JoinPollGroup(code) and update on every "ReceiveVoteUpdate" push
 * Cleans up (LeavePollGroup + stop) on unmount / code change.
 */
export function useLiveResults(pollCode: string) {
  const [results, setResults] = useState<VoteResults | null>(null);
  const [loading, setLoading] = useState(true);
  const [notFound, setNotFound] = useState(false);
  const [connected, setConnected] = useState(false);
  const connectionRef = useRef<HubConnection | null>(null);

  useEffect(() => {
    let cancelled = false;

    // 1. Initial snapshot
    api
      .get<VoteResults>(`/polls/${pollCode}/results`)
      .then((r) => {
        if (!cancelled) setResults(r.data);
      })
      .catch((e) => {
        if (!cancelled && axios.isAxiosError(e) && e.response?.status === 404) {
          setNotFound(true);
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    // 2. Live connection
    const connection = new HubConnectionBuilder()
      .withUrl(HUB_URL)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
    connectionRef.current = connection;

    // 3. Live updates
    connection.on('ReceiveVoteUpdate', (updated: VoteResults) => {
      if (!cancelled) setResults(updated);
    });

    connection
      .start()
      .then(() => {
        if (cancelled) return;
        setConnected(true);
        return connection.invoke('JoinPollGroup', pollCode);
      })
      .catch((err) => console.error('SignalR connection failed:', err));

    // Cleanup
    return () => {
      cancelled = true;
      const conn = connectionRef.current;
      connectionRef.current = null;
      if (!conn) return;
      if (conn.state === 'Connected') {
        conn.invoke('LeavePollGroup', pollCode).finally(() => conn.stop());
      } else {
        conn.stop();
      }
    };
  }, [pollCode]);

  return { results, loading, notFound, connected };
}

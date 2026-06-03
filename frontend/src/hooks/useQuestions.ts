import { useEffect, useRef, useState } from 'react';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import api from '../api/api';
import type { Question } from '../types/poll.types';

const HUB_URL = import.meta.env.VITE_HUB_URL ?? 'http://localhost:5000/hubs/poll';

/**
 * Live anonymous Q&A: fetches the question list, then keeps it in sync via the SignalR
 * "ReceiveQuestionsUpdate" broadcast. Mutations (submit/upvote/pin) post to the API; the
 * server re-broadcasts the refreshed list, so we never set local state imperatively.
 */
export function useQuestions(pollCode: string) {
  const [questions, setQuestions] = useState<Question[]>([]);
  const connectionRef = useRef<HubConnection | null>(null);

  useEffect(() => {
    let cancelled = false;

    api
      .get<Question[]>(`/polls/${pollCode}/questions`)
      .then((r) => {
        if (!cancelled) setQuestions(r.data);
      })
      .catch(() => {});

    const connection = new HubConnectionBuilder()
      .withUrl(HUB_URL)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
    connectionRef.current = connection;

    connection.on('ReceiveQuestionsUpdate', (updated: Question[]) => {
      if (!cancelled) setQuestions(updated);
    });

    connection
      .start()
      .then(() => {
        if (cancelled) return;
        return connection.invoke('JoinPollGroup', pollCode);
      })
      .catch((err) => console.error('Q&A SignalR connection failed:', err));

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

  const submit = (text: string) => api.post(`/polls/${pollCode}/questions`, { text });
  const upvote = (id: string) => api.post(`/polls/${pollCode}/questions/${id}/upvote`);
  const pin = (id: string) => api.post(`/polls/${pollCode}/questions/${id}/pin`);

  return { questions, submit, upvote, pin };
}

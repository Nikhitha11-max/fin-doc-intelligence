import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface AnswerCitation {
  documentId: string;
  documentTitle: string;
  chunkId: string;
  pageStart: number | null;
  pageEnd: number | null;
  sheetName: string | null;
  rowStart: number | null;
  rowEnd: number | null;
}

export interface AnswerResponse {
  answer: string;
  insufficientEvidence: boolean;
  citations: AnswerCitation[];
  conversationId: string;
}

export interface ConversationMessage {
  id: string;
  role: 'user' | 'assistant';
  text: string;
  createdAt: string;
  citations: AnswerCitation[];
}

export interface Conversation {
  id: string;
  title: string;
  createdAt: string;
  updatedAt: string;
  messages: ConversationMessage[];
}

export interface ConversationListItem {
  id: string;
  title: string;
  createdAt: string;
  updatedAt: string;
  messageCount: number;
}

export interface ConversationPage {
  items: ConversationListItem[];
  total: number;
  page: number;
  pageSize: number;
}

@Injectable({ providedIn: 'root' })
export class AnswersService {
  private readonly http = inject(HttpClient);

  ask(question: string, conversationId?: string): Observable<AnswerResponse> {
    const body = conversationId ? { question, conversationId } : { question };
    return this.http.post<AnswerResponse>('/api/answers', body);
  }

  list(page = 1, pageSize = 20): Observable<ConversationPage> {
    return this.http.get<ConversationPage>('/api/conversations', { params: { page, pageSize } });
  }

  get(id: string): Observable<Conversation> {
    return this.http.get<Conversation>(`/api/conversations/${encodeURIComponent(id)}`);
  }
}

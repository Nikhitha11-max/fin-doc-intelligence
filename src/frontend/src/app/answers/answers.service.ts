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
}

@Injectable({ providedIn: 'root' })
export class AnswersService {
  private readonly http = inject(HttpClient);

  ask(question: string): Observable<AnswerResponse> {
    return this.http.post<AnswerResponse>('/api/answers', { question });
  }
}

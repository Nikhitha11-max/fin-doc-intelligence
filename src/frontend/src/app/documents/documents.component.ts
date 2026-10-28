import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { AuthService } from '../core/auth.service';
import { ALLOWED_EXTENSIONS, DocumentItem, DocumentsService, MAX_UPLOAD_BYTES } from './documents.service';

const PAGE_SIZE = 10;

@Component({
  selector: 'app-documents',
  imports: [DatePipe],
  templateUrl: './documents.component.html',
  styleUrl: './documents.component.scss',
})
export class DocumentsComponent implements OnInit {
  private readonly api = inject(DocumentsService);
  private readonly auth = inject(AuthService);

  readonly items = signal<DocumentItem[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly loading = signal(false);
  readonly message = signal('');
  readonly error = signal('');
  readonly editingId = signal<string | null>(null);

  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.total() / PAGE_SIZE)));
  readonly accept = ALLOWED_EXTENSIONS.join(',');

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.list(this.page(), PAGE_SIZE).subscribe({
      next: (result) => {
        this.items.set(result.items);
        this.total.set(result.total);
        this.loading.set(false);
      },
      error: (e) => this.fail(e, 'Could not load documents.'),
    });
  }

  goTo(page: number): void {
    this.page.set(Math.min(Math.max(page, 1), this.totalPages()));
    this.load();
  }

  upload(input: HTMLInputElement, title: HTMLInputElement): void {
    const file = input.files?.[0];
    if (!file) {
      return;
    }
    // Early feedback only; the server validates type, size and content.
    const extension = file.name.slice(file.name.lastIndexOf('.')).toLowerCase();
    if (!ALLOWED_EXTENSIONS.includes(extension)) {
      this.error.set('Only PDF, CSV and XLSX files are supported.');
      return;
    }
    if (file.size === 0 || file.size > MAX_UPLOAD_BYTES) {
      this.error.set('The file must be between 1 byte and 25 MB.');
      return;
    }
    this.clearMessages();
    this.api.upload(file, title.value).subscribe({
      next: () => {
        input.value = '';
        title.value = '';
        this.message.set('Document uploaded.');
        this.page.set(1);
        this.load();
      },
      error: (e) => this.fail(e, 'Upload failed.'),
    });
  }

  download(doc: DocumentItem): void {
    this.clearMessages();
    this.api.download(doc.id).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = doc.fileName;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: (e) => this.fail(e, 'Download failed.'),
    });
  }

  saveTitle(doc: DocumentItem, value: string): void {
    this.clearMessages();
    this.api.rename(doc.id, value).subscribe({
      next: () => {
        this.editingId.set(null);
        this.load();
      },
      error: (e) => this.fail(e, 'Rename failed.'),
    });
  }

  remove(doc: DocumentItem): void {
    if (!confirm(`Delete "${doc.title}"?`)) {
      return;
    }
    this.clearMessages();
    this.api.delete(doc.id).subscribe({
      next: () => {
        this.message.set('Document deleted.');
        if (this.items().length === 1 && this.page() > 1) {
          this.page.update((p) => p - 1);
        }
        this.load();
      },
      error: (e) => this.fail(e, 'Delete failed.'),
    });
  }

  logout(): void {
    this.auth.logout();
  }

  formatSize(bytes: number): string {
    return bytes < 1024 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB`;
  }

  private clearMessages(): void {
    this.message.set('');
    this.error.set('');
  }

  private fail(e: HttpErrorResponse, fallback: string): void {
    this.loading.set(false);
    if (e.status === 403) {
      this.error.set('You do not have permission to do that.');
    } else if (e.status === 404) {
      this.error.set('That document no longer exists.');
      this.load();
    } else if (e.status === 400 && typeof e.error?.detail === 'string') {
      this.error.set(e.error.detail);
    } else {
      this.error.set(fallback);
    }
  }
}

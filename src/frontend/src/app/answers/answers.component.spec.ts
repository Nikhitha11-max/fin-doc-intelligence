import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { AnswersComponent } from './answers.component';

describe('AnswersComponent', () => {
  let fixture: ComponentFixture<AnswersComponent>;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(AnswersComponent);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  it('shows an answer and its page citation', () => {
    fixture.componentInstance.ask('  revenue increased  ');
    const request = http.expectOne('/api/answers');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ question: 'revenue increased' });
    request.flush({
      answer: 'Revenue increased by 10 percent.',
      insufficientEvidence: false,
      citations: [{
        documentId: 'd1', documentTitle: 'Quarterly report', chunkId: 'c1',
        pageStart: 3, pageEnd: 4, sheetName: null, rowStart: null, rowEnd: null,
      }],
    });
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent;
    expect(text).toContain('Revenue increased by 10 percent.');
    expect(text).toContain('Quarterly report');
    expect(text).toContain('Pages 3–4');
  });

  it('shows insufficient evidence without displaying citations', () => {
    fixture.componentInstance.ask('unsupported fact');
    http.expectOne('/api/answers').flush({
      answer: "I couldn't find enough relevant information.",
      insufficientEvidence: true,
      citations: [],
    });
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent;
    expect(text).toContain("I couldn't find enough relevant information.");
    expect(text).not.toContain('Sources');
  });

  it('does not submit an empty question', () => {
    fixture.componentInstance.ask('  ');

    http.expectNone('/api/answers');
    expect(fixture.componentInstance.error()).toBe('Enter a question.');
  });

  it('shows a safe provider-unavailable message', () => {
    fixture.componentInstance.ask('revenue');
    http.expectOne('/api/answers').flush(
      { detail: 'The answer provider is temporarily unavailable.' },
      { status: 503, statusText: 'Service Unavailable' },
    );

    expect(fixture.componentInstance.error()).toBe('The answer provider is temporarily unavailable.');
  });
});

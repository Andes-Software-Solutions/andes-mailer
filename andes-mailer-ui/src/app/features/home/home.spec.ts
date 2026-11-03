import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Home } from './home';

describe('Home', () => {
  let fixture: ComponentFixture<Home>;
  let backend: HttpTestingController;

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });

    backend = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(Home);
    TestBed.tick();
  });

  it('renders the product heading', async () => {
    backend.expectOne('/api/users/me').flush({ id: '1', name: 'Ada Lovelace' });
    await fixture.whenStable();

    expect(element().querySelector('h1')?.textContent).toBe('Andes Mailer');
  });

  it('shows who is signed in', async () => {
    backend.expectOne('/api/users/me').flush({ id: '1', name: 'Ada Lovelace' });
    await fixture.whenStable();

    expect(element().textContent).toContain('Signed in as Ada Lovelace');
  });

  it('says so when the account cannot be loaded', async () => {
    backend
      .expectOne('/api/users/me')
      .flush(null, { status: 401, statusText: 'Unauthorized' });
    await fixture.whenStable();

    expect(element().querySelector('[role=alert]')?.textContent).toContain(
      "We couldn't load your account.",
    );
  });
});

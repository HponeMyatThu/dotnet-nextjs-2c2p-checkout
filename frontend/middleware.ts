import { NextRequest, NextResponse } from 'next/server';

export function middleware(request: NextRequest) {
  // 2C2P redirects back to frontendReturnUrl via an HTML POST form submission.
  // Next.js App Router pages only handle GET and fail on cross-origin POST (Origin: null).
  // Intercept POST on /result and convert it to a 303 See Other GET redirect.
  if (request.method === 'POST' && request.nextUrl.pathname === '/result') {
    const targetUrl = request.nextUrl.clone();
    return NextResponse.redirect(targetUrl, 303);
  }

  return NextResponse.next();
}

export const config = {
  matcher: '/result',
};

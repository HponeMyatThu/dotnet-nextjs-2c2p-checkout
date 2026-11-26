import type { Metadata } from 'next';
import './globals.css';

export const metadata: Metadata = {
  title: 'Rezerv Payment Checkout',
  description: '2C2P Payment Integration Demo',
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body className="min-h-screen bg-gray-50">{children}</body>
    </html>
  );
}

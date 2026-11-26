// API types matching backend DTOs

export type PaymentType = 'OneTime' | 'Recurring';
export type TransactionStatus = 'Pending' | 'Succeeded' | 'Failed';

export interface Customer {
  id: number;
  firstName: string;
  lastName: string;
  email: string;
  fullName: string;
}

export interface Product {
  id: number;
  name: string;
  description: string;
  price: number;
  currency: string;
  paymentType: PaymentType;
}

export interface CustomerCard {
  id: number;
  customerId: number;
  maskedCardNo: string;
  createdAt: string;
}

export interface CheckoutRequest {
  customerId: number;
  productId: number;
  customerCardId: number | null;
}

export interface CheckoutResponse {
  invoiceNo: string;
  status: TransactionStatus;
  redirectUrl: string | null;
}

export interface Transaction {
  id: number;
  customerId: number;
  customerName: string;
  productId: number;
  productName: string;
  invoiceNo: string;
  idempotencyKey: string;
  amount: number;
  currency: string;
  status: TransactionStatus;
  paymentChannel: string | null;
  maskedCardNo: string | null;
  providerRef: string | null;
  respCode: string | null;
  respDesc: string | null;
  redirectUrl: string | null;
  createdAt: string;
  paidAt: string | null;
}

export interface ApiError {
  status: number;
  title: string;
  detail: string;
  instance: string;
  errors?: Record<string, string[]>;
}

import {
  Customer,
  Product,
  CustomerCard,
  CheckoutRequest,
  CheckoutResponse,
  Transaction,
  ApiError,
} from '@/types/api';

const API_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000';

async function fetchJson<T>(url: string, options?: RequestInit): Promise<T> {
  const response = await fetch(url, options);
  const data = await response.json();
  if (!response.ok) {
    throw data as ApiError;
  }
  return data as T;
}

export async function fetchCustomers(): Promise<Customer[]> {
  return fetchJson<Customer[]>(`${API_URL}/api/customers`);
}

export async function fetchProducts(): Promise<Product[]> {
  return fetchJson<Product[]>(`${API_URL}/api/products`);
}

export async function fetchCustomerCards(customerId: number): Promise<CustomerCard[]> {
  return fetchJson<CustomerCard[]>(`${API_URL}/api/customers/${customerId}/cards`);
}

export async function checkout(
  idempotencyKey: string,
  request: CheckoutRequest
): Promise<CheckoutResponse> {
  return fetchJson<CheckoutResponse>(`${API_URL}/api/checkout`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Idempotency-Key': idempotencyKey,
    },
    body: JSON.stringify(request),
  });
}

export const processCheckout = checkout;

export async function fetchTransaction(invoiceNo: string): Promise<Transaction> {
  return fetchJson<Transaction>(`${API_URL}/api/transactions/${invoiceNo}`);
}

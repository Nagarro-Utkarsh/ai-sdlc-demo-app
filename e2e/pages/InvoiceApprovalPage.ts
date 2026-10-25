import { expect, type Page } from '@playwright/test';

export class InvoiceApprovalPage {
  constructor(private readonly page: Page) {}

  async signIn(username: string, password: string): Promise<void> {
    await this.page.getByLabel('Username').fill(username);
    await this.page.getByLabel('Password').fill(password);
    await this.page.getByRole('button', { name: 'Sign in' }).click();
    await expect(this.page.getByRole('tab', { name: 'Invoices' })).toBeVisible();
  }

  async createAndSubmitInvoice(customerName: string, amount: number): Promise<void> {
    await this.page.getByLabel('Customer name').fill(customerName);
    await this.page.getByRole('spinbutton', { name: 'Amount' }).fill(String(amount));
    await this.page.getByRole('button', { name: 'Create invoice' }).click();

    const invoiceRow = this.page.getByRole('row').filter({ hasText: customerName });
    const submitButton = invoiceRow.getByRole('button', { name: /Submit invoice/ });
    await expect(submitButton).toBeVisible();
    await submitButton.click();
    await expect(invoiceRow).toContainText('Pending Approval');
  }

  async openApprovals(): Promise<void> {
    await this.page.getByRole('tab', { name: 'Approvals' }).click();
  }

  async approveInvoice(customerName: string): Promise<void> {
    const invoiceApproval = this.page.getByRole('listitem').filter({ hasText: customerName });
    await invoiceApproval.getByRole('button', { name: /Approve invoice/ }).click();
  }

  async approveInvoicePartially(customerName: string, approvedAmount: number, note: string): Promise<void> {
    const invoiceApproval = this.page.getByRole('listitem').filter({ hasText: customerName });
    await invoiceApproval.getByLabel(/Approved amount for/).fill(String(approvedAmount));
    await invoiceApproval.getByLabel(/Approval note for/).fill(note);
    await invoiceApproval.getByRole('button', { name: /Approve invoice/ }).click();
  }

  async rejectInvoice(customerName: string, reason: string): Promise<void> {
    const invoiceApproval = this.page.getByRole('listitem').filter({ hasText: customerName });
    await invoiceApproval.getByLabel(/Rejection reason for/).fill(reason);
    await invoiceApproval.getByRole('button', { name: /Reject invoice/ }).click();
  }

  async signOut(): Promise<void> {
    await this.page.getByRole('button', { name: 'Sign out' }).click();
    await expect(this.page.getByRole('button', { name: 'Sign in' })).toBeVisible();
  }

  async expectInvoiceStatus(customerName: string, status: string, approvedAmount?: string): Promise<void> {
    await this.openInvoicesAfterPause();
    const invoiceRow = this.page.getByRole('row').filter({ hasText: customerName });
    await expect(invoiceRow).toContainText(status);
    if (approvedAmount) await expect(invoiceRow).toContainText(approvedAmount);
  }

  async openInvoicesAfterPause(): Promise<void> {
    const pauseMs = Number(process.env.PLAYWRIGHT_SLOW_MO ?? 0);
    if (pauseMs > 0) await this.page.waitForTimeout(pauseMs);
    await this.page.getByRole('tab', { name: 'Invoices' }).click();
  }

  async expectRejectedApproval(customerName: string): Promise<void> {
    await expect(this.page.getByRole('alert')).toContainText('Manager cannot approve');
    await this.openInvoicesAfterPause();
    await expect(this.page.getByRole('row').filter({ hasText: customerName }))
      .toContainText('Pending Approval');
  }
}

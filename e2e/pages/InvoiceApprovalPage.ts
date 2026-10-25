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

  async expectRejectedApproval(customerName: string): Promise<void> {
    await expect(this.page.getByRole('alert')).toContainText('Manager cannot approve');
    await this.page.getByRole('tab', { name: 'Invoices' }).click();
    await expect(this.page.getByRole('row').filter({ hasText: customerName }))
      .toContainText('Pending Approval');
  }
}

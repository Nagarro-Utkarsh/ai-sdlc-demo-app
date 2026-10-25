import { expect, test } from '@playwright/test';
import { InvoiceApprovalPage } from '../pages/InvoiceApprovalPage';

test('shows an over-limit approval reason and keeps the invoice pending', async ({ page }) => {
  const customerName = `Approval limit test ${Date.now()}`;
  const approvals = new InvoiceApprovalPage(page);

  await page.goto('/');
  await approvals.signIn('demo.manager', 'ChangeMe123!');
  await approvals.createAndSubmitInvoice(customerName, 100_000);
  await approvals.openApprovals();
  await approvals.approveInvoice(customerName);

  await approvals.expectRejectedApproval(customerName);
});

test('records and displays a partial approval amount and note', async ({ page }) => {
  const customerName = `Partial approval test ${Date.now()}`;
  const approvalNote = 'One invoice line is disputed';
  const approvals = new InvoiceApprovalPage(page);

  await page.goto('/');
  await approvals.signIn('demo.manager', 'ChangeMe123!');
  await approvals.createAndSubmitInvoice(customerName, 75_000);
  await approvals.openApprovals();
  await approvals.approveInvoicePartially(customerName, 50_000, approvalNote);

  await page.getByRole('tab', { name: 'Invoices' }).click();
  const invoiceRow = page.getByRole('row').filter({ hasText: customerName });
  await expect(invoiceRow).toContainText('Partially Approved');
  await expect(invoiceRow).toContainText('$75,000.00');
  await expect(invoiceRow).toContainText('$50,000.00');
  await expect(invoiceRow).toContainText(approvalNote);
});

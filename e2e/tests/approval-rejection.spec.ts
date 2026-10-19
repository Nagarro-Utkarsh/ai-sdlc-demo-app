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

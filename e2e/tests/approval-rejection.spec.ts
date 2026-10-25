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

  await approvals.openInvoicesAfterPause();
  const invoiceRow = page.getByRole('row').filter({ hasText: customerName });
  await expect(invoiceRow).toContainText('Partially Approved');
  await expect(invoiceRow).toContainText('$75,000.00');
  await expect(invoiceRow).toContainText('$50,000.00');
  await expect(invoiceRow).toContainText(approvalNote);
});

test('approves an invoice at the inclusive Manager limit', async ({ page }) => {
  const customerName = `Manager limit approval ${Date.now()}`;
  const approvals = new InvoiceApprovalPage(page);

  await page.goto('/');
  await approvals.signIn('demo.manager', 'ChangeMe123!');
  await approvals.createAndSubmitInvoice(customerName, 50_000);
  await approvals.openApprovals();
  await approvals.approveInvoice(customerName);

  await approvals.expectInvoiceStatus(customerName, 'Approved', '$50,000.00');
});

test('approves an invoice at the inclusive Director limit', async ({ page }) => {
  const customerName = `Director limit approval ${Date.now()}`;
  const approvals = new InvoiceApprovalPage(page);

  await page.goto('/');
  await approvals.signIn('demo.director', 'ChangeMe123!');
  await approvals.createAndSubmitInvoice(customerName, 500_000);
  await approvals.openApprovals();
  await approvals.approveInvoice(customerName);

  await approvals.expectInvoiceStatus(customerName, 'Approved', '$500,000.00');
});

test('requires a Manager followed by a different Director for high-value approval', async ({ page }) => {
  const customerName = `Two approvers ${Date.now()}`;
  const approvals = new InvoiceApprovalPage(page);

  await page.goto('/');
  await approvals.signIn('demo.manager', 'ChangeMe123!');
  await approvals.createAndSubmitInvoice(customerName, 600_000);
  await approvals.openApprovals();
  await approvals.approveInvoice(customerName);

  const approvalItem = page.getByRole('listitem').filter({ hasText: customerName });
  await expect(approvalItem).toContainText('Awaiting approval from a different Director');
  await expect(approvalItem).toBeVisible();

  await approvals.signOut();
  await approvals.signIn('demo.director', 'ChangeMe123!');
  await approvals.openApprovals();
  await approvals.approveInvoice(customerName);

  await approvals.expectInvoiceStatus(customerName, 'Approved', '$600,000.00');
});

test('does not allow a Director to start a high-value approval', async ({ page }) => {
  const customerName = `Director first approval ${Date.now()}`;
  const approvals = new InvoiceApprovalPage(page);

  await page.goto('/');
  await approvals.signIn('demo.director', 'ChangeMe123!');
  await approvals.createAndSubmitInvoice(customerName, 600_000);
  await approvals.openApprovals();
  await approvals.approveInvoice(customerName);

  await expect(page.getByRole('alert')).toContainText('Director cannot complete a high-value invoice without a prior Manager approval');
  await approvals.expectInvoiceStatus(customerName, 'Pending Approval');
});

test('requires a reason to reject and records a valid Manager rejection', async ({ page }) => {
  const customerName = `Manager rejection ${Date.now()}`;
  const rejectionReason = 'The invoice does not match the purchase order';
  const approvals = new InvoiceApprovalPage(page);

  await page.goto('/');
  await approvals.signIn('demo.manager', 'ChangeMe123!');
  await approvals.createAndSubmitInvoice(customerName, 25_000);
  await approvals.openApprovals();
  await approvals.rejectInvoice(customerName, '');

  await expect(page.getByRole('alert')).toContainText('A reason is required to reject an invoice.');
  await approvals.expectInvoiceStatus(customerName, 'Pending Approval');

  await approvals.openApprovals();
  await approvals.rejectInvoice(customerName, rejectionReason);
  await approvals.expectInvoiceStatus(customerName, 'Rejected');
});

test('allows Admin to reject a pending invoice but not approve it', async ({ page }) => {
  const customerName = `Admin rejection ${Date.now()}`;
  const rejectionReason = 'Supporting documents are missing';
  const approvals = new InvoiceApprovalPage(page);

  await page.goto('/');
  await approvals.signIn('demo.manager', 'ChangeMe123!');
  await approvals.createAndSubmitInvoice(customerName, 25_000);
  await approvals.signOut();
  await approvals.signIn('demo.admin', 'ChangeMe123!');
  await approvals.openApprovals();
  await approvals.approveInvoice(customerName);

  await expect(page.getByRole('alert')).toContainText('This action requires one of these roles: Manager, Director.');
  await approvals.expectInvoiceStatus(customerName, 'Pending Approval');

  await approvals.openApprovals();
  await approvals.rejectInvoice(customerName, rejectionReason);
  await approvals.expectInvoiceStatus(customerName, 'Rejected');
});

test('requires a note for a partial approval and leaves the invoice pending when it is missing', async ({ page }) => {
  const customerName = `Partial approval needs note ${Date.now()}`;
  const approvals = new InvoiceApprovalPage(page);

  await page.goto('/');
  await approvals.signIn('demo.manager', 'ChangeMe123!');
  await approvals.createAndSubmitInvoice(customerName, 75_000);
  await approvals.openApprovals();
  const approvalItem = page.getByRole('listitem').filter({ hasText: customerName });
  await approvalItem.getByLabel(/Approved amount for/).fill('50000');
  await approvals.approveInvoice(customerName);

  await expect(page.getByRole('alert')).toContainText('A note is required when approving less than the invoiced amount.');
  await approvals.expectInvoiceStatus(customerName, 'Pending Approval');
});

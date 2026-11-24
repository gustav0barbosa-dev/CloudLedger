import { expect, Page, test } from '@playwright/test';

async function login(page: Page): Promise<void> {
  await page.goto('/login');
  await page.locator('input[name="email"]').fill('demo@cloudledger.dev');
  await page.locator('input[name="password"]').fill('Demo@12345');
  await page.getByRole('button', { name: 'Entrar' }).click();
  await expect(page).toHaveURL(/\/dashboard$/);
}

test.beforeEach(async ({ page }) => {
  await login(page);
});

test('cadastro, edição e exclusão de categoria personalizada', async ({ page }) => {
  await page.goto('/categories');

  const name = `Categoria E2E ${Date.now()}`;
  await page.locator('input[placeholder="Nome da categoria"]').fill(name);
  await page.getByRole('button', { name: 'Criar', exact: true }).click();

  let row = page.locator('tbody tr', { hasText: name });
  await expect(row).toBeVisible();

  const renamed = `${name} Editada`;
  await row.getByRole('button', { name: 'Editar' }).click();
  await page.locator('input[placeholder="Nome da categoria"]').fill(renamed);
  await page.getByRole('button', { name: 'Salvar' }).click();

  row = page.locator('tbody tr', { hasText: renamed });
  await expect(row).toBeVisible();

  page.once('dialog', dialog => void dialog.accept());
  await row.getByRole('button', { name: 'Excluir' }).click();
  await expect(row).toHaveCount(0);
});

test('telas de apoio executam ações principais', async ({ page }) => {
  await page.goto('/goals');
  let goalRow = page.locator('tbody tr', { hasText: 'Viagem de férias' });
  await goalRow.getByRole('button', { name: 'Movimentos' }).click();
  await page.locator('input[placeholder="Valor"]').fill('10');
  await page.getByRole('button', { name: 'Aportar' }).click();
  await expect(page.getByText('Aporte registrado.')).toBeVisible();

  await page.goto('/recurring');
  const recurringRow = page.locator('tbody tr', { hasText: 'Spotify' }).first();
  await recurringRow.getByRole('button', { name: 'Ocorrências' }).click();
  await page.locator('tr.details').getByRole('button', { name: 'Pagar' }).first().click();
  await expect(page.getByText('Ocorrência paga. Lançamento gerado.')).toBeVisible();

  await page.goto('/notifications');
  await expect(page.getByRole('heading', { name: 'Notificações' })).toBeVisible();
  await page.getByRole('button', { name: 'Marcar todas como lidas' }).click();
  await expect(page.getByText(/marcada\(s\) como lida\(s\)/)).toBeVisible();

  await page.goto('/audit');
  await expect(page.getByRole('heading', { name: 'Auditoria' })).toBeVisible();
  await expect(page.locator('.error-box')).toHaveCount(0);
  await expect(page.locator('tbody tr').first()).toBeVisible();

  await page.goto('/reports');
  await page.getByRole('button', { name: 'Exportar CSV' }).click();
  await expect(page.getByRole('link', { name: 'Baixar arquivo' })).toBeVisible();

  await page.goto('/profile');
  await expect(page.locator('input[type="email"]')).toHaveValue('demo@cloudledger.dev');

  await page.goto('/settings');
  await expect(page.getByRole('heading', { name: 'Configurações' })).toBeVisible();
});

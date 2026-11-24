import { expect, test } from '@playwright/test';

test('login de demonstração abre o dashboard do protótipo', async ({ page }) => {
  await page.goto('/login');

  await page.locator('input[name="email"]').fill('demo@cloudledger.dev');
  await page.locator('input[name="password"]').fill('Demo@12345');
  await page.getByRole('button', { name: 'Entrar' }).click();

  await expect(page).toHaveURL(/\/dashboard$/);
  await expect(page.getByRole('heading', { name: 'Visão geral' })).toBeVisible();
  const cards = page.locator('app-summary-cards');
  await expect(cards.getByText('Saldo total')).toBeVisible();
  await expect(cards.getByText('Receitas')).toBeVisible();
  await expect(cards.getByText('Despesas')).toBeVisible();
  await expect(cards.getByText('Economia')).toBeVisible();
  await expect(page.getByText('Evolução financeira')).toBeVisible();
  await expect(page.getByText('Transações recentes')).toBeVisible();
});

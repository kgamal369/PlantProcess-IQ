import { test, expect } from '@playwright/test';

test.describe('PlantProcess IQ full-stack smoke', () => {
  
  test('frontend opens and backend health responds', async ({ page, request }) => {
    // 1. Authenticate using the exact development credentials
    const loginRes = await request.post('http://localhost:5063/auth/login', {
      data: { 
        UserName: 'admin', 
        Password: 'ChangeMe123!' 
      }
    });
    
    expect(loginRes.ok()).toBeTruthy();
    const data = await loginRes.json();
    const token = data.accessToken;

    // 2. Assert backend telemetry status is operational
    const response = await request.get('http://localhost:5063/health', {
      headers: { Authorization: `Bearer ${token}` }
    });
    expect(response.ok()).toBeTruthy();

    // 3. Confirm front-end web shell loads and intercepts successfully
    await page.goto('/');
    await expect(page).toHaveTitle(/PlantProcess IQ/i);
  });

});
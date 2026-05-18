import { test, expect } from '@playwright/test';

// Helper function to authenticate using your exact appsettings configurations
async function getAuthToken(request: any) {
  const loginRes = await request.post('http://localhost:5063/auth/login', {
    data: { 
      UserName: 'admin', 
      Password: 'ChangeMe123!' 
    }
  });
  
  // Ensure the login succeeds against the live API framework
  expect(loginRes.ok()).toBeTruthy();
  
  const data = await loginRes.json();
  return data.accessToken;
}

test.describe('PlantProcess IQ backend API smoke', () => {
  
  test('health endpoint responds', async ({ request }) => {
    const token = await getAuthToken(request);
    const response = await request.get('http://localhost:5063/health', {
      headers: { Authorization: `Bearer ${token}` }
    });
    expect(response.ok()).toBeTruthy();
  });

  test('swagger document is generated in development', async ({ request }) => {
    const token = await getAuthToken(request);
    const response = await request.get('http://localhost:5063/swagger/v1/swagger.json', {
      headers: { Authorization: `Bearer ${token}` }
    });
    expect(response.ok()).toBeTruthy();
  });

  test('admin jobs monitor endpoint responds', async ({ request }) => {
    const token = await getAuthToken(request);
    const response = await request.get('http://localhost:5063/admin/jobs-monitor', {
      headers: { Authorization: `Bearer ${token}` }
    });
    expect(response.ok()).toBeTruthy();
    
    const body = await response.json();
    expect(body).toBeDefined();
  });

});
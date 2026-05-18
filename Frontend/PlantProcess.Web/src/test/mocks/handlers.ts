import { http, HttpResponse } from "msw";

export const handlers = [
  http.get("*/health", () => {
    return HttpResponse.json({
      service: "PlantProcess IQ API",
      status: "Healthy",
      utc: new Date().toISOString()
    });
  }),

  http.get("*/admin/jobs-monitor", () => {
    return HttpResponse.json({
      summary: {
        totalJobs: 0,
        runningJobs: 0,
        failedJobs: 0,
        healthyJobs: 0
      },
      jobs: []
    });
  })
];

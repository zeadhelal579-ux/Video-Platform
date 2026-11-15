// Public credentials and build-time selection for the standalone demo.
export const isDemoMode = import.meta.env.VITE_DEMO_MODE !== "false";
export const demoUser = { id: "videoplatform-demo", email: "demo@videoplatform.example" };
export const demoPassword = "Demo1234!";

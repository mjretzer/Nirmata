import { RouterProvider } from "react-router";
import { router } from "./router";
import { Toaster } from "./components/ui/sonner";
import { VerificationProvider } from "./context/VerificationContext";
import { WorkspaceProvider } from "./context/WorkspaceContext";
import { ThemeProvider } from "next-themes";

export default function App() {
  return (
    <ThemeProvider attribute="class" defaultTheme="dark">
      <WorkspaceProvider>
        <VerificationProvider>
          <RouterProvider router={router} />
          <Toaster position="bottom-right" />
        </VerificationProvider>
      </WorkspaceProvider>
    </ThemeProvider>
  );
}
import "../global.css";
import { useEffect, useMemo } from "react";
import { useAppStore, useHydration } from "@/stores";
import FontAwesome from "@expo/vector-icons/FontAwesome";
import { ThemeProvider as NavigationThemeProvider } from "@react-navigation/native";
import { useFonts } from "expo-font";
import { Stack } from "expo-router";
import * as SplashScreen from "expo-splash-screen";
import { StatusBar } from "expo-status-bar";
import { PaperProvider } from "react-native-paper";
import { SafeAreaProvider } from "react-native-safe-area-context";
import "react-native-reanimated";
import { DarkTheme, LightTheme } from "@/configs/themes";

function RootLayout() {
  const { activeTheme, inverseTheme, addSystemThemeListener } = useAppStore((state) => state.appearance);
  const themeConfig = useMemo(() => (activeTheme == "dark" ? DarkTheme : LightTheme), [activeTheme]);

  useEffect(() => {
    const systemThemeListener = addSystemThemeListener();
    return () => systemThemeListener.remove();
  }, []);

  return (
    <SafeAreaProvider>
      <PaperProvider theme={themeConfig}>
        <NavigationThemeProvider value={themeConfig}>
          <StatusBar style={inverseTheme} />
          <Stack>
            <Stack.Screen name="index" options={{ headerShown: false }} />
            <Stack.Screen name="(auth)" options={{ headerShown: false }} />
          </Stack>
        </NavigationThemeProvider>
      </PaperProvider>
    </SafeAreaProvider>
  );
}

export { ErrorBoundary } from "expo-router";

SplashScreen.preventAutoHideAsync();

export default function Layout() {
  const hydrated = useHydration();

  const [loaded, error] = useFonts({
    SpaceMono: require("../assets/fonts/SpaceMono-Regular.ttf"),
    ...FontAwesome.font
  });

  useEffect(() => {
    if (error) throw error;
  }, [error]);

  useEffect(() => {
    if (loaded && hydrated) {
      SplashScreen.hideAsync();
    }
  }, [loaded, hydrated]);

  if (!loaded || !hydrated) {
    return null;
  }

  return <RootLayout />;
}

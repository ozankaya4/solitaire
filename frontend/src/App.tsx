import { useState } from 'react';
import { AnimatePresence, motion, useReducedMotion } from 'motion/react';
import type { ScreenName, VariantId } from './app/types';
import { GameScreen } from './components/board/GameScreen';
import { MainMenu } from './components/MainMenu';
import { SavedGamesScreen } from './components/SavedGamesScreen';
import { SettingsScreen } from './components/SettingsScreen';
import { TopBar } from './components/TopBar';

export default function App() {
  const [screen, setScreen] = useState<ScreenName>('menu');
  // When set, the board resumes this specific variant (e.g. from Saved games);
  // otherwise it uses the settings default.
  const [resumeVariant, setResumeVariant] = useState<VariantId | undefined>(undefined);
  const reduceMotion = useReducedMotion();

  const openGame = (variant?: VariantId) => {
    setResumeVariant(variant);
    setScreen('game');
  };

  // The board is full-screen with its own control bar (no global top bar).
  if (screen === 'game') {
    return <GameScreen onExit={() => setScreen('menu')} initialVariant={resumeVariant} />;
  }

  const transition = reduceMotion
    ? { duration: 0 }
    : { duration: 0.28, ease: [0.16, 0.9, 0.1, 1] as const };
  const enter = reduceMotion ? {} : { opacity: 0, y: 10 };
  const exit = reduceMotion ? {} : { opacity: 0, y: -8 };

  return (
    <div className="shell">
      <TopBar onHome={() => setScreen('menu')} />

      <AnimatePresence mode="wait" initial={false}>
        <motion.main
          key={screen}
          initial={enter}
          animate={{ opacity: 1, y: 0 }}
          exit={exit}
          transition={transition}
          style={{ display: 'flex', flex: 1, flexDirection: 'column' }}
        >
          {screen === 'menu' && (
            <MainMenu
              onPlay={() => openGame(undefined)}
              onOpenSettings={() => setScreen('settings')}
              onOpenSaved={() => setScreen('saved')}
            />
          )}
          {screen === 'settings' && <SettingsScreen onBack={() => setScreen('menu')} />}
          {screen === 'saved' && (
            <SavedGamesScreen onBack={() => setScreen('menu')} onResume={(v) => openGame(v)} />
          )}
        </motion.main>
      </AnimatePresence>
    </div>
  );
}

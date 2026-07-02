import { useState } from 'react';
import { AnimatePresence, motion, useReducedMotion } from 'motion/react';
import type { ScreenName } from './app/types';
import { GameScreen } from './components/board/GameScreen';
import { MainMenu } from './components/MainMenu';
import { SavedGamesScreen } from './components/SavedGamesScreen';
import { SettingsScreen } from './components/SettingsScreen';
import { TopBar } from './components/TopBar';

export default function App() {
  const [screen, setScreen] = useState<ScreenName>('menu');
  const reduceMotion = useReducedMotion();

  // The board is full-screen with its own control bar (no global top bar).
  if (screen === 'game') {
    return <GameScreen onExit={() => setScreen('menu')} />;
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
              onPlay={() => setScreen('game')}
              onOpenSettings={() => setScreen('settings')}
              onOpenSaved={() => setScreen('saved')}
            />
          )}
          {screen === 'settings' && <SettingsScreen onBack={() => setScreen('menu')} />}
          {screen === 'saved' && <SavedGamesScreen onBack={() => setScreen('menu')} />}
        </motion.main>
      </AnimatePresence>
    </div>
  );
}

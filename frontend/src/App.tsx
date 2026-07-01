import { motion } from 'motion/react';
import { useTranslation } from 'react-i18next';

// Skeleton shell only — real game UI will replace this.
export default function App() {
  const { t } = useTranslation();

  return (
    <motion.main
      className="app"
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      transition={{ duration: 0.3 }}
    >
      <h1>{t('app.title', 'Solitaire')}</h1>
    </motion.main>
  );
}

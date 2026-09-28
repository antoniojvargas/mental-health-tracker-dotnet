import eslint from '@eslint/js'
import prettier from 'eslint-config-prettier'
import vue from 'eslint-plugin-vue'
import tseslint from 'typescript-eslint'

export default tseslint.config(
  {
    ignores: [
      'dist/**',
      'coverage/**',
      'node_modules/**',
      // Generado por `npm run generate:types`: no es código nuestro.
      'src/types/api.generated.d.ts',
    ],
  },
  eslint.configs.recommended,
  ...tseslint.configs.recommended,
  ...vue.configs['flat/recommended'],
  {
    // Para el bloque `<script lang="ts">` de un SFC, vue-eslint-parser tiene que
    // delegar en el parser de TypeScript; sin esto solo se parsea el template.
    files: ['**/*.vue'],
    languageOptions: {
      parserOptions: { parser: tseslint.parser },
    },
  },
  {
    // prettier ya formatea, así que las reglas de estilo de ESLint sobran.
    files: ['**/*.{js,ts,vue}'],
    ...prettier,
  },
  {
    rules: {
      '@typescript-eslint/no-unused-vars': [
        'error',
        { argsIgnorePattern: '^_', varsIgnorePattern: '^_' },
      ],
    },
  },
)

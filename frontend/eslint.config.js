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
    // Globals del navegador para todo `src/`. La app corre en el navegador y
    // nada en el repo ejecuta código de navegador en Node, así que `no-undef` sobre
    // `document` o `KeyboardEvent` no está señalando un error real: señala que la
    // lista de globals no la tiene en cuenta. Antes no se notaba porque el
    // frontend no tocaba el DOM; el primer componente que lo hace, el Modal, lo
    // destapa de golpe.
    //
    // Escrita a mano y no con el paquete `globals` a propósito: son veinte
    // entradas que no van a cambiar, y añadir una dependencia para tener una
    // lista constante no compensa. Si algún día hace falta el paquete, este
    // bloque es lo que se sustituye.
    files: ['src/**/*.{ts,vue}'],
    languageOptions: {
      globals: {
        document: 'readonly',
        window: 'readonly',
        navigator: 'readonly',
        location: 'readonly',
        history: 'readonly',
        localStorage: 'readonly',
        sessionStorage: 'readonly',
        requestAnimationFrame: 'readonly',
        cancelAnimationFrame: 'readonly',
        setTimeout: 'readonly',
        clearTimeout: 'readonly',
        setInterval: 'readonly',
        clearInterval: 'readonly',
        Event: 'readonly',
        CustomEvent: 'readonly',
        HTMLElement: 'readonly',
        HTMLButtonElement: 'readonly',
        HTMLInputElement: 'readonly',
        Element: 'readonly',
        Node: 'readonly',
        NodeList: 'readonly',
        KeyboardEvent: 'readonly',
        FocusEvent: 'readonly',
        MouseEvent: 'readonly',
        EventTarget: 'readonly',
        getComputedStyle: 'readonly',
      },
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
  {
    // El UI kit se llama por lo que hace, no por un prefijo: `Button`, no
    // `UiButton`. Un `Button` dentro de `components/ui/` no colisiona con nada,
    // mientras que el prefijo redundaría con la carpeta. La regla se relaja solo
    // aquí, para que siga marcando error en el resto de la app, donde un nombre
    // corto sí puede perderse en un `import`.
    files: ['src/components/ui/**/*.vue'],
    rules: {
      'vue/multi-word-component-names': 'off',
    },
  },
)

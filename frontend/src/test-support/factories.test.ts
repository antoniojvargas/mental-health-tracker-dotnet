import { describe, expect, it } from 'vitest'

import { makeLog, makeUser } from './factories'

describe('makeLog', () => {
  it('devuelve un registro completo con id derivado de la fecha', () => {
    const log = makeLog()

    expect(log.logDate).toBe('2026-08-01')
    expect(log.id).toBe('id-2026-08-01')
    expect(log.symptoms).toEqual([])
    expect(log.sleepDisturbances).toEqual([])
  })

  it('el id sigue a logDate, salvo que se indique otro', () => {
    expect(makeLog({ logDate: '2026-10-03' }).id).toBe('id-2026-10-03')
    expect(makeLog({ logDate: '2026-10-03', id: 'custom' }).id).toBe('custom')
  })

  it('sobrescribe solo lo indicado', () => {
    const log = makeLog({ moodRating: 5, notes: 'hola' })

    expect(log).toMatchObject({ moodRating: 5, notes: 'hola', anxietyLevel: 5, stressLevel: 5 })
  })

  it('cada llamada devuelve objetos y listas independientes', () => {
    const first = makeLog()
    const second = makeLog()

    first.symptoms.push({ type: 'panic', severity: 1 })

    expect(second.symptoms).toEqual([])
    expect(second).not.toBe(first)
  })
})

describe('makeUser', () => {
  it('devuelve un usuario completo y sobrescribe lo indicado', () => {
    expect(makeUser()).toEqual({
      id: 'user-1',
      email: 'ana@example.com',
      name: 'Ana Gómez',
      avatarUrl: null,
    })
    expect(makeUser({ name: 'Luis' })).toMatchObject({ name: 'Luis', email: 'ana@example.com' })
  })
})

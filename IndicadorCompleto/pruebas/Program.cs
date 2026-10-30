// Pruebas del núcleo de IndicadorCompleto, sin ATAS ni Windows. Devuelve el número de fallos (0 = todo bien).
var c = new Comprobador();

PruebasComponentes.Ejecutar(c);
PruebasModelos.Ejecutar(c);

return c.Resumen();

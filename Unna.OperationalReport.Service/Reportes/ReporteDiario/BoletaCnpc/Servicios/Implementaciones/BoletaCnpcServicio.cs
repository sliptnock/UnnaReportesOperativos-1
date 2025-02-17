﻿using DocumentFormat.OpenXml.Office2010.ExcelAc;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Newtonsoft.Json;
using System.Drawing.Printing;
using Unna.OperationalReport.Data.Auth.Enums;
using Unna.OperationalReport.Data.Fuentes.Repositorios.Abstracciones;
using Unna.OperationalReport.Data.Fuentes.Repositorios.Implementaciones;
using Unna.OperationalReport.Data.Registro.Enums;
using Unna.OperationalReport.Data.Registro.Procedimientos;
using Unna.OperationalReport.Data.Registro.Repositorios.Abstracciones;
using Unna.OperationalReport.Data.Reporte.Enums;
using Unna.OperationalReport.Service.Reportes.Generales.Dtos;
using Unna.OperationalReport.Service.Reportes.Generales.Servicios.Abstracciones;
using Unna.OperationalReport.Service.Reportes.Impresiones.Dtos;
using Unna.OperationalReport.Service.Reportes.Impresiones.Servicios.Abstracciones;
using Unna.OperationalReport.Service.Reportes.ReporteDiario.BoletaCnpc.Dtos;
using Unna.OperationalReport.Service.Reportes.ReporteDiario.BoletaCnpc.Servicios.Abstracciones;
using Unna.OperationalReport.Service.Reportes.ReporteDiario.BoletaVentaGns.Dtos;
using Unna.OperationalReport.Tools.Comunes.Infraestructura.Dtos;
using Unna.OperationalReport.Tools.Comunes.Infraestructura.Utilitarios;

namespace Unna.OperationalReport.Service.Reportes.ReporteDiario.BoletaCnpc.Servicios.Implementaciones
{
    public class BoletaCnpcServicio : IBoletaCnpcServicio
    {



        private readonly IDiaOperativoRepositorio _diaOperativoRepositorio;
        private readonly IRegistroRepositorio _registroRepositorio;
        private readonly IBoletaCnpcVolumenComposicionGnaEntradaRepositorio _boletaCnpcVolumenComposicionGnaEntradaRepositorio;
        private readonly IReporteServicio _reporteServicio;
        private readonly IGnsVolumeMsYPcBrutoRepositorio _gnsVolumeMsYPcBrutoRepositorio;
        private readonly IImpresionServicio _impresionServicio;
        public BoletaCnpcServicio(
            IDiaOperativoRepositorio diaOperativoRepositorio,
            IRegistroRepositorio registroRepositorio,
            IBoletaCnpcVolumenComposicionGnaEntradaRepositorio boletaCnpcVolumenComposicionGnaEntradaRepositorio,
            IReporteServicio reporteServicio,
            IGnsVolumeMsYPcBrutoRepositorio gnsVolumeMsYPcBrutoRepositorio,
            IImpresionServicio impresionServicio
            )
        {
            _diaOperativoRepositorio = diaOperativoRepositorio;
            _registroRepositorio = registroRepositorio;
            _boletaCnpcVolumenComposicionGnaEntradaRepositorio = boletaCnpcVolumenComposicionGnaEntradaRepositorio;
            _reporteServicio = reporteServicio;
            _gnsVolumeMsYPcBrutoRepositorio = gnsVolumeMsYPcBrutoRepositorio;
            _impresionServicio = impresionServicio;
        }

        public async Task<OperacionDto<BoletaCnpcDto>> ObtenerAsync(long idUsuario)
        {

            var operacionGeneral = await _reporteServicio.ObtenerAsync((int)TiposReportes.BoletaCnpc, idUsuario);
            if (!operacionGeneral.Completado)
            {
                return new OperacionDto<BoletaCnpcDto>(CodigosOperacionDto.NoExiste, operacionGeneral.Mensajes);
            }

            var operacionImpresion = await _impresionServicio.ObtenerAsync((int)TiposReportes.BoletaCnpc, FechasUtilitario.ObtenerDiaOperativo());
            if (operacionImpresion.Completado && operacionImpresion.Resultado != null && !string.IsNullOrWhiteSpace(operacionImpresion.Resultado.Datos))
            {
                var rpta = JsonConvert.DeserializeObject<BoletaCnpcDto>(operacionImpresion.Resultado.Datos);
                rpta.General = operacionGeneral.Resultado;
                return new OperacionDto<BoletaCnpcDto>(rpta);
            }


            DateTime diaOperativo = FechasUtilitario.ObtenerDiaOperativo();
            BoletaCnpcTabla1Dto tabla1 = new BoletaCnpcTabla1Dto();

            double gasMpcd1 = 0;
            double gasMpcd2 = 0;
            var primerDato = await _diaOperativoRepositorio.ObtenerPorIdLoteYFechaAsync((int)TiposLote.LoteIv, FechasUtilitario.ObtenerDiaOperativo(), (int)TipoGrupos.FiscalizadorEnel, (int)TiposNumeroRegistro.PrimeroRegistro);
            if (primerDato != null)
            {
                var dato = await _registroRepositorio.ObtenerPorIdDatoYDiaOperativoAsync((int)TiposDatos.VolumenMpcd, primerDato.IdDiaOperativo);
                if (dato != null)
                {
                    gasMpcd1 = dato.Valor ?? 0;
                }
            }
            var segundoDato = await _diaOperativoRepositorio.ObtenerPorIdLoteYFechaAsync((int)TiposLote.LoteIv, FechasUtilitario.ObtenerDiaOperativo(), (int)TipoGrupos.FiscalizadorEnel, (int)TiposNumeroRegistro.SegundoRegistro);
            if (segundoDato != null)
            {
                var dato = await _registroRepositorio.ObtenerPorIdDatoYDiaOperativoAsync((int)TiposDatos.CnpcPeruGnaRecibido, segundoDato.IdDiaOperativo);
                if (dato != null)
                {
                    gasMpcd2 = dato.Valor ?? 0;
                }
            }

            var dto = new BoletaCnpcDto
            {
                Fecha = diaOperativo.ToString("dd/MM/yyyy")
            };

            dto.General = operacionGeneral.Resultado;

            // tabla N° 01

            tabla1.Fecha = dto.Fecha;
            tabla1.GasMpcd = Math.Round(gasMpcd1, 0) - Math.Round(gasMpcd2);
            tabla1.GlpBls = 13;
            tabla1.CgnBls = 14;
            dto.Tabla1 = tabla1;


            //Cuadro N° 1. Fiscalización de GNS del GAS Adicional del Lote X
            var volumenTotalGns = await _gnsVolumeMsYPcBrutoRepositorio.ObtenerPorTipoYNombreDiaOperativoAsync(TiposTablasSupervisorPgt.VolumenMsGnsAgpsa, TiposGnsVolumeMsYPcBruto.GnsAEgpsa, diaOperativo);
            if (volumenTotalGns != null)
            {
                dto.VolumenTotalGnsEnMs = volumenTotalGns.VolumeMs;
            }
            dto.FlareGna = 0;// falta dato                        
            dto.FactoresDistribucionGasNaturalSeco = await FactoresDistribucionGasNaturalSeco();


            #region Cuadro N° 2. Asignación de Gas Combustible al GNA Adicional del Lote X
<<<<<<< HEAD
            var factoresDistribucionGasDeCombustible = new List<FactoresDistribucionGasNaturalDto>();
            factoresDistribucionGasDeCombustible.Add(new FactoresDistribucionGasNaturalDto
            {
                Item = 1,
                Suministrador = "LOTE Z69",
                Volumen = 500
            });
            factoresDistribucionGasDeCombustible.Add(new FactoresDistribucionGasNaturalDto
            {
                Item = 2,
                Suministrador = "CNPC",
                Volumen = 122
            });
            dto.FactoresDistribucionGasNaturalSeco = factoresDistribucionGasDeCombustible;
=======

            var entidadLotes = await _registroRepositorio.BoletaCnpcFactoresDistribucionDeGasCombustibleAsync(diaOperativo);

            dto.FactoresDistribucionGasDeCombustible = FactoresDistribucionGasNatural(entidadLotes);
            #endregion


            #region Cuadro N° 3. Asignación de LGN al GNA Adicional del Lote X

            dto.VolumenProduccionTotalGlp = 0;
            dto.VolumenProduccionTotalCgn = 0;
            dto.VolumenProduccionTotalLgn = 0;
            dto.FactoresDistribucionLiquidoGasNatural = FactoresDistribucionLiquidoGasNatural(entidadLotes, dto.VolumenProduccionTotalLgn ?? 0);
            dto.GravedadEspecifica = 0.5359;
            dto.VolumenProduccionTotalGlpCnpc = 0;
            dto.VolumenProduccionTotalCgnCnpc = 0;
>>>>>>> 167c2705d9de82a64af34163f144962ed3f817f8
            #endregion


            return new OperacionDto<BoletaCnpcDto>(dto);
        }


        // tabla N 01 - Factores de Distribución de Gas Natural Seco
        private async Task<List<FactoresDistribucionGasNaturalDto>> FactoresDistribucionGasNaturalSeco()
        {

            var entidades = await _boletaCnpcVolumenComposicionGnaEntradaRepositorio.ListarAsync();


            List<FactoresDistribucionGasNaturalDto> factoresDistribucionGasNaturalSeco = new List<FactoresDistribucionGasNaturalDto>();

            factoresDistribucionGasNaturalSeco.Add(new FactoresDistribucionGasNaturalDto
            {
                Item = 1,
                Suministrador = "LOTE Z69",
                Volumen = 0,
                ConcentracionC1 = entidades.Where(e => e.Id == 1).FirstOrDefault() != null ? entidades.Where(e => e.Id == 1).First().ConcentracionN2HastaO2 : 0,
                VolumenC1 = 1,
                FactoresDistribucion = 1,
                AsignacionGns = 1,
            });
            factoresDistribucionGasNaturalSeco.Add(new FactoresDistribucionGasNaturalDto
            {
                Item = 2,
                Suministrador = "CNPC",
                Volumen = 13,
                ConcentracionC1 = entidades.Where(e => e.Id == 1).FirstOrDefault() != null ? entidades.Where(e => e.Id == 1).First().ConcentracionN2HastaO2 : 0,
                VolumenC1 = 1,
                FactoresDistribucion = 1,
                AsignacionGns = 1,
            });
            factoresDistribucionGasNaturalSeco.Add(new FactoresDistribucionGasNaturalDto
            {
                Item = 3,
                Suministrador = "LOTE VI",
                Volumen = 0,
                ConcentracionC1 = entidades.Where(e => e.Id == 3).FirstOrDefault() != null ? entidades.Where(e => e.Id == 3).First().ConcentracionN2HastaO2 : 0,
                VolumenC1 = 1,
                FactoresDistribucion = 1,
                AsignacionGns = 1,
            });
            factoresDistribucionGasNaturalSeco.Add(new FactoresDistribucionGasNaturalDto
            {
                Item = 4,
                Suministrador = "LOTE I",
                Volumen = 0,
                ConcentracionC1 = entidades.Where(e => e.Id == 4).FirstOrDefault() != null ? entidades.Where(e => e.Id == 4).First().ConcentracionN2HastaO2 : 0,

                FactoresDistribucion = 1,
                AsignacionGns = 1,
            });
            factoresDistribucionGasNaturalSeco.Add(new FactoresDistribucionGasNaturalDto
            {
                Item = 5,
                Suministrador = "LOTE IV",
                Volumen = 0,
                ConcentracionC1 = entidades.Where(e => e.Id == 5).FirstOrDefault() != null ? entidades.Where(e => e.Id == 5).First().ConcentracionN2HastaO2 : 0,

                FactoresDistribucion = 1,
                AsignacionGns = 1,
            });
            factoresDistribucionGasNaturalSeco.Add(new FactoresDistribucionGasNaturalDto
            {
                Item = 6,
                Suministrador = "CNPC ADICIONAL",
                Volumen = 353,
                ConcentracionC1 = entidades.Where(e => e.Id == 6).FirstOrDefault() != null ? entidades.Where(e => e.Id == 6).First().ConcentracionN2HastaO2 : 0,
                //VolumenC1 = Volumen + ConcentracionC1,
                FactoresDistribucion = 1,
                AsignacionGns = 1,
            });
            factoresDistribucionGasNaturalSeco.Add(new FactoresDistribucionGasNaturalDto
            {
                Suministrador = "Total",
                Volumen = factoresDistribucionGasNaturalSeco.Sum(e => e.Volumen),
                ConcentracionC1 = factoresDistribucionGasNaturalSeco.Sum(e => e.ConcentracionC1),
                VolumenC1 = factoresDistribucionGasNaturalSeco.Sum(e => e.VolumenC1),
                FactoresDistribucion = factoresDistribucionGasNaturalSeco.Sum(e => e.FactoresDistribucion),
                AsignacionGns = factoresDistribucionGasNaturalSeco.Sum(e => e.AsignacionGns),
            });

            return factoresDistribucionGasNaturalSeco;
        }



        // Cuadro N° 2. Asignación de Gas Combustible al GNA Adicional del Lote X
        private List<FactoresDistribucionGasNaturalDto> FactoresDistribucionGasNatural(List<BoletaCnpcFactoresDistribucionDeGasCombustible> entidad)
        {

            var lista = new List<FactoresDistribucionGasNaturalDto>();
            lista = entidad.Select(e => new FactoresDistribucionGasNaturalDto
            {
                Item = e.IdLote,
                Sumistrador = e.Lote,
                Volumen = e.Volumen,
                AsignacionGns = e.AsignacionGns,
                ConcentracionC1 = e.ConcentracionC1,
                FactoresDistribucion = e.FactoresDistribucion,
                VolumenC1 = e.VolumenC1
            }).ToList();

            lista.Add(new FactoresDistribucionGasNaturalDto
            {
                Item = (lista.Count + 1),
                Sumistrador = "Total",
                Volumen = lista.Sum(e => e.Volumen),
                ConcentracionC1 = Math.Round(lista.Sum(e => e.VolumenConcentracionC1) ?? 0 / lista.Sum(e => e.Volumen) ?? 0, 4),
                VolumenC1 = lista.Sum(e => e.VolumenC1),
                FactoresDistribucion = lista.Sum(e => e.FactoresDistribucion),
                AsignacionGns = lista.Sum(e => e.AsignacionGns),
            });
            return lista;
        }


        // Cuadro  Cuadro N° 3. Asignación de LGN al GNA Adicional del Lote X
        private List<FactoresDistribucionLiquidoGasNaturalDto> FactoresDistribucionLiquidoGasNatural(List<BoletaCnpcFactoresDistribucionDeGasCombustible> entidad, double volumenProduccionTotalLgn)
        {

            var lista = new List<FactoresDistribucionLiquidoGasNaturalDto>();
            lista = entidad.Select(e => new FactoresDistribucionLiquidoGasNaturalDto
            {
                Item = e.IdLote,
                Sumistrador = e.Lote,
                Volumen = e.Volumen??0,
                Riqueza = e.Riqueza??0,
                Contenido = Math.Round(e.Volumen ?? 0 * e.Riqueza ?? 0, 3)
            }).ToList();

            double sumaContenido = lista.Sum(e => e.Contenido);

            lista.ForEach(e => e.FactoresDistribucion = Math.Round((e.Contenido / sumaContenido) * 100, 4));
            lista.ForEach(e => e.AsignacionGns = Math.Round(volumenProduccionTotalLgn * e.FactoresDistribucion, 2));

            lista.Add(new FactoresDistribucionLiquidoGasNaturalDto
            {
                Item = (lista.Count + 1),
                Sumistrador = "Total",
                Volumen = lista.Sum(e => e.Volumen),
                Riqueza = Math.Round(lista.Sum(e => e.VolumenRiqueza) / lista.Sum(e => e.Volumen), 4),
                Contenido = lista.Sum(e => e.Contenido),
                FactoresDistribucion = lista.Sum(e => e.FactoresDistribucion),
                AsignacionGns = lista.Sum(e => e.AsignacionGns),
            });
            return lista;
        }







        public async Task<OperacionDto<RespuestaSimpleDto<bool>>> GuardarAsync(BoletaCnpcDto peticion)
        {
            var operacionValidacion = ValidacionUtilitario.ValidarModelo<RespuestaSimpleDto<bool>>(peticion);
            if (!operacionValidacion.Completado)
            {
                return operacionValidacion;
            }
            peticion.General = null;
            var dto = new ImpresionDto()
            {
                IdConfiguracion = RijndaelUtilitario.EncryptRijndaelToUrl((int)TiposReportes.BoletaCnpc),
                Fecha = FechasUtilitario.ObtenerDiaOperativo(),
                IdUsuario = peticion.IdUsuario,
                Datos = JsonConvert.SerializeObject(peticion)
            };
            return await _impresionServicio.GuardarAsync(dto);

        }

    }
}

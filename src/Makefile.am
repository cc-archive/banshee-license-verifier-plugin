MCS_FLAGS = -debug
ASSEMBLY = LicenseVerifier.dll
SOURCES = \
	$(srcdir)/LicenseVerifierPlugin.cs

plugindir = $(PLUGINDIR)
plugin_SCRIPTS = $(ASSEMBLY) $(ASSEMBLY).mdb

all: $(ASSEMBLY)

$(ASSEMBLY): $(SOURCES)
	$(MCS) $(MCS_FLAGS) -out:$(ASSEMBLY) -r:$(top_builddir)/cc-sharp/cc-sharp.dll -r:System.Data -target:library $(BANSHEE_LIBS) $(SOURCES)

EXTRA_DIST = $(SOURCES)
CLEANFILES = $(ASSEMBLY) $(ASSEMBLY).mdb 
DISTCLEANFILES = Makefile.in 


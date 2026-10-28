package com.bajoterra.core.content;

import java.io.IOException;
import java.net.JarURLConnection;
import java.net.URISyntaxException;
import java.net.URL;
import java.net.URLConnection;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.Enumeration;
import java.util.List;
import java.util.jar.JarEntry;
import java.util.jar.JarFile;
import java.util.stream.Stream;

/**
 * Lists the {@code .json} files of a resource directory across every classpath root (directories and jars), so
 * content packs shipped in other jars are discovered without any registration step.
 */
final class ResourceScanner {

    /** A JSON resource: its file name and where to read it from. */
    record Resource(String name, URL url) {
    }

    private ResourceScanner() {
        throw new AssertionError("Utility class, not instantiable");
    }

    static List<Resource> listJson(ClassLoader loader, String directory) {
        List<Resource> result = new ArrayList<>();
        try {
            Enumeration<URL> roots = loader.getResources(directory);
            while (roots.hasMoreElements()) {
                URL root = roots.nextElement();
                if ("file".equals(root.getProtocol())) {
                    listDirectory(root, result);
                } else if ("jar".equals(root.getProtocol())) {
                    listJar(loader, root, directory, result);
                }
            }
        } catch (IOException | URISyntaxException e) {
            throw new ContentException("Cannot scan content directory '" + directory + "'", e);
        }
        result.sort((a, b) -> a.name().compareTo(b.name()));
        return result;
    }

    private static void listDirectory(URL root, List<Resource> result) throws IOException, URISyntaxException {
        try (Stream<Path> files = Files.list(Path.of(root.toURI()))) {
            for (Path file : (Iterable<Path>) files::iterator) {
                Path name = file.getFileName();
                if (name != null && name.toString().endsWith(".json")) {
                    result.add(new Resource(name.toString(), file.toUri().toURL()));
                }
            }
        }
    }

    private static void listJar(ClassLoader loader, URL root, String directory, List<Resource> result)
            throws IOException {
        URLConnection connection = root.openConnection();
        if (!(connection instanceof JarURLConnection jarConnection)) {
            return;
        }
        jarConnection.setUseCaches(false);
        try (JarFile jar = jarConnection.getJarFile()) {
            String prefix = directory.endsWith("/") ? directory : directory + "/";
            Enumeration<JarEntry> entries = jar.entries();
            while (entries.hasMoreElements()) {
                String name = entries.nextElement().getName();
                if (name.startsWith(prefix) && name.endsWith(".json") && name.indexOf('/', prefix.length()) < 0) {
                    URL url = loader.getResource(name);
                    if (url != null) {
                        result.add(new Resource(name.substring(prefix.length()), url));
                    }
                }
            }
        }
    }
}
